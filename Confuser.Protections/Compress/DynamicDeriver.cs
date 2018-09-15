﻿using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Compress {
	internal sealed class DynamicDeriver : IKeyDeriver {
		StatementBlock derivation;
		Action<uint[], uint[]> encryptFunc;

		public void Init(IConfuserContext ctx, IRandomGenerator random) {
			ctx.Registry.GetRequiredService<IDynCipherService>().GenerateCipherPair(random, out derivation, out var dummy);

			var dmCodeGen = new DMCodeGen(typeof(void), new[] {
				Tuple.Create("{BUFFER}", typeof(uint[])),
				Tuple.Create("{KEY}", typeof(uint[]))
			});
			dmCodeGen.GenerateCIL(derivation);
			encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();
		}

		uint[] IKeyDeriver.DeriveKey(uint[] a, uint[] b) {
			var ret = new uint[0x10];
			Buffer.BlockCopy(a, 0, ret, 0, a.Length * sizeof(uint));
			encryptFunc(ret, b);
			return ret;
		}

		CryptProcessor IKeyDeriver.EmitDerivation(IConfuserContext ctx) => (method, block, key) => {
			var ret = new List<Instruction>();
			var codeGen = new CodeGen(block, key, method, ret);
			codeGen.GenerateCIL(derivation);
			codeGen.Commit(method.Body);
			return ret;
		};

		private sealed class CodeGen : CILCodeGen {
			private readonly Local block;
			private readonly Local key;

			internal CodeGen(Local block, Local key, MethodDef method, IList<Instruction> instrs)
				: base(method, instrs) {
				this.block = block;
				this.key = key;
			}

			protected override Local Var(Variable var) {
				switch (var.Name) {
					case "{BUFFER}": return block;
					case "{KEY}": return key;
					default: return base.Var(var);
				}
			}
		}
	}
}
