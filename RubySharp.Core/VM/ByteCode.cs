using System;
using System.Collections.Generic;

namespace RubySharp.Core {
	public class ByteCode {

		public FastList< Instr > Code = new FastList< Instr > ();
		public VM vm;

		public ByteCode ( VM vm ) {
			this.vm = vm;
		}
		
		public void DebugTravel () {
			foreach ( var instr in Code ) {
				Console.WriteLine ( instr );
			}
		}

		internal Instr AppendInstruction ( Instr c ) {
			Code.Add ( c );
			return c;
		}
		
		internal int Push ( int n = 1 ) {
			Scope s = vm.CurrentScope;
			if ( s.sp + n >= Scope.MAX_STACK_SIZE ) {
				Console.WriteLine ( $"too complex expression ({s.sp} + {n})" );
			}
			s.sp += n;
			return s.sp;
		}

		internal int Pop ( int n = 1 ) {
			Scope s = vm.CurrentScope;
			if ( s.sp - n < 0 ) {
				Console.WriteLine ( $"stack pointer underflow ({s.sp} - {n})" );
			}
			s.sp -= n;
			return s.sp;
		}

		internal Instr GenOp_0 ( OpCode op ) {
			vm.CurrentScope.lastpc = vm.CurrentScope.pc;
			var instr = AppendInstruction ( new Instr ( op ) );
			vm.CurrentScope.pc++;
			return instr;
		}

		internal Instr GenOp_1 ( OpCode op, int a ) {
			vm.CurrentScope.lastpc = vm.CurrentScope.pc;
			var instr = AppendInstruction ( new Instr ( op, a ) );
			vm.CurrentScope.pc++;
			return instr;
		}

		internal Instr GenOp_2 ( OpCode op, int a, int b ) {
			vm.CurrentScope.lastpc = vm.CurrentScope.pc;
			var instr = AppendInstruction ( new Instr ( op, a, b ) );
			vm.CurrentScope.pc++;
			return instr;
		}

		internal Instr GenOp_3 ( OpCode op, int a, int b, int c ) {
			vm.CurrentScope.lastpc = vm.CurrentScope.pc;
			var instr = AppendInstruction ( new Instr ( op, a, b, c ) );
			vm.CurrentScope.pc++;
			return instr;
		}

		internal Instr GenJmp ( OpCode op, int pc, out int pos ) {
			vm.CurrentScope.lastpc = vm.CurrentScope.pc;
			Instr instr = AppendInstruction ( new Instr ( op, pc ) );
			vm.CurrentScope.pc++;
			pos = vm.CurrentScope.pc;

			return instr;
		}

		internal Instr GenMove ( int dst, int src, bool nopeep ) {

			if ( true ) {
				goto normal;
			}
			else {
				Scope s = vm.CurrentScope;
				Instr data = s.Last;
				switch ( data.insn ) {
					case OpCode.Move:
						if ( dst == src ) return null;             /* remove useless MOVE */
						if ( data.b == dst && data.a == src ) /* skip swapping MOVE */
							return null;
						goto normal;
					case OpCode.LoadNIL:
					case OpCode.LoadSelf:
						if ( nopeep || data.a != src /*|| data.a < s->nlocals*/ ) goto normal;
						s.pc = s.lastpc;
						GenOp_1 ( data.insn, dst );
						break;
					case OpCode.LoadI:
					case OpCode.GetGV:
					case OpCode.GetIV:
					case OpCode.GetCV:
					case OpCode.GetConst:
					case OpCode.String:
					case OpCode.Lambda:
					case OpCode.Block:
					case OpCode.Method:
						if ( nopeep || data.a != src /*|| data.a < s.nlocals*/ ) goto normal;
						s.pc = s.lastpc;
						GenOp_2 ( data.insn, dst, data.b );
						break;
					default:
						goto normal;
				}
			}
			return null;

normal:
			return AppendInstruction ( new Instr ( OpCode.Move, dst, src ) );

			// TODO: on_eval
			//return;
		}

		internal void GenReturn ( Scope s, OpCode op, int src ) {
			if ( no_peephole ( s ) ) {
				GenOp_1 ( op, src );
			}
			else {

				Instr data = s.Last;

				if ( data.insn == OpCode.Move && src == data.a ) {
					s.pc = s.lastpc;
					GenOp_1 ( op, data.b );
				}
				else if ( data.insn != OpCode.Return ) {
					GenOp_1 ( op, src);
				}
			}
		}

		internal Instr GenAssignment ( AstNode tree, int sp ) {
			
			int idx;
			
			switch ( tree.type ) {
				case AstNodeType.GVAR:
					string name = ( (INamedNode)tree ).name;
					Scope s = vm.TopScope;
			
					if ( !s.HasValue ( name ) ) {
						s.AddLocalValue ( name );
					}

					idx = s.GetLocalValueIdx ( name );
					return AppendInstruction ( new Instr ( OpCode.SetGV, vm.CurrentScope.sp, idx ) );
				case AstNodeType.ARG:
				case AstNodeType.LVAR:
					break;
				case AstNodeType.IVAR:
					break;
				case AstNodeType.CVAR:
					break;
				case AstNodeType.CONST:
					break;
				case AstNodeType.COLON2:
					break;
				case AstNodeType.CALL:
				case AstNodeType.SCALL:
					break;
				case AstNodeType.MASGN:
					break;
				case AstNodeType.NIL:
					break;
				default:
					Console.WriteLine ( $"unknown lhs {tree}" );
					break;
			}

			return null;
		}

		internal void GenValues ( AstNode tree, out int n ) {

			n = 0;

			// TODO: splat mode
			switch ( tree.type ) {
				case AstNodeType.ARRAY:
					var arrayNode = tree.As<ArrayNode> ();
					foreach ( var node in arrayNode.nodes ) {
						node.Compile ( this );
						n++;
					}
					break;
				case AstNodeType.ARGS:
					var argsNode = tree.As<ArgsNode> ();
					foreach ( var node in argsNode.args ) {
						node.Compile ( this );
						n++;
					}
					break;
			}
		}

		internal int GenScopeBody ( Scope s, AstNode tree ) {
			Scope bodyScope = vm.CreateScope ( s );
			vm.BeginScope ( bodyScope );

			tree.Compile ( this );
			GenReturn ( s, OpCode.Return, s.sp - 1 );

			vm.FinishScope ( bodyScope );

			// return ????
			return bodyScope.pc;
		}

		public Instr Emit_Literal ( AstNode node ) {

			Value val = Value.Nil ();

			switch ( node.type ) {
				case AstNodeType.INT:
					val = node.As<IntNode> ().value;
					break;
				case AstNodeType.FLOAT:
					val = node.As<FloatNode> ().value;
					break;
				case AstNodeType.STR:
					val = node.As<StringNode> ().value;
					break;
				case AstNodeType.TRUE:
					val = node.As<TrueNode> ().value;
					break;
				case AstNodeType.FALSE:
					val = node.As<FalseNode> ().value;
					break;
				case AstNodeType.NIL:
					val = Value.Nil ();
					break;
				default:
					Console.WriteLine ( $"Emit_Literal unknown type: {node.type}" );
					break;
			}

			vm.PushValue ( val );

			if ( node.type == AstNodeType.NIL ) {
				return AppendInstruction ( new Instr ( OpCode.LoadNIL, vm.CurrentScope.sp ) );
			}

			if ( node.type == AstNodeType.TRUE ) {
				return AppendInstruction ( new Instr ( OpCode.LoadT, vm.CurrentScope.sp ) );
			}

			if ( node.type == AstNodeType.FALSE ) {
				return AppendInstruction ( new Instr ( OpCode.LoadF, vm.CurrentScope.sp ) );
			}

			return AppendInstruction ( new Instr ( OpCode.LoadI, vm.CurrentScope.sp, vm.ValueStackTopIndex ) );
		}

		public Instr Emit_Self ( AstNode node ) {
			var instr = AppendInstruction ( new Instr ( OpCode.LoadSelf, vm.CurrentScope.sp ) );
			Push ();
			return instr;
		}

		public Instr Emit_LocalVar ( AstNode node ) {

			string name = ( (INamedNode)node ).name;
			Scope s = vm.CurrentScope;

			// mruby在解析语法树时已经创建局部变量了, 与mruby不同的是我们需要在编译阶段动态添加
			if ( !s.HasValue ( name ) ) {
				s.AddLocalValue ( name );
			}

			int idx = s.GetLocalValueIdx ( name );

			if ( idx != -1 ) {
				return GenMove ( s.sp, idx, true );
				//if ( val != 0 && s.parser.on_eval ) GenOp_0 ( s, OpCode.Nop );
			}
			else {
				int lv = 0;
				Scope up = s.prev;

				while ( up != null ) {
					idx = up.GetLocalValueIdx ( ( (INamedNode)node ).name );
					if ( idx != -1 ) {
						return GenOp_3 ( OpCode.GetUpVar, s.sp, idx, lv );
						break;
					}

					lv++;
					up = up.prev;
				}
			}

			return null;
			//return AppendInstruction ( new Instr ( OpCode.LoadL, vm.CurrentScope.sp ) );
		}
		
		public Instr Emit_GlobalVar ( AstNode node ) {
			
			string name = ( (INamedNode)node ).name;
			Scope  s    = vm.TopScope;
			
			if ( !s.HasValue ( name ) ) {
				s.AddLocalValue ( name );
			}

			int idx = s.GetLocalValueIdx ( name );

			var instr = GenMove ( s.sp, idx, true );
			Push ();
			return instr;
			//if ( val != 0 && s.parser.on_eval ) GenOp_0 ( s, OpCode.Nop );
		}
		
		public Instr Emit_InstanceVar ( AstNode node ) {

			string name = ( (INamedNode)node ).name;
			// RObject obj = vm.CurrentScope.self.p as RObject;
			
			// vm.PushValue ( obj.GetIV ( name ) );
			
			var instr = AppendInstruction ( new Instr ( OpCode.GetIV, vm.CurrentScope.sp, vm.Symbol ( name ) ) );
			Push ();
			return instr;
		}
		
		public Instr Emit_ClassVar ( AstNode node ) {

			string name = ( (INamedNode)node ).name;
			// RObject obj  = vm.CurrentScope.self.p as RObject;
			
			// vm.PushValue ( obj.c.GetCV ( name ) );
			var instr = AppendInstruction ( new Instr ( OpCode.GetCV, vm.CurrentScope.sp, vm.Symbol ( name ) ) );
			Push ();
			return instr;
		}
		
		public Instr Emit_ConstVar ( AstNode node ) {

			string name = ( (INamedNode)node ).name;
			
			var instr = AppendInstruction ( new Instr ( OpCode.GetConst, vm.CurrentScope.sp, vm.Symbol ( name ) ) );
			Push ();
			return instr;
		}
		
		public Instr Emit_Symbol ( AstNode node ) {

			string name = ( (INamedNode)node ).name;
			// RObject obj = vm.CurrentScope.self.p as RObject;
			
			// vm.PushValue ( obj.GetIV ( name ) );
			var instr = AppendInstruction ( new Instr ( OpCode.LoadSYM, vm.CurrentScope.sp, vm.Symbol ( name ) ) );
			Push ();
			return instr;
		}

		public Instr Emit_Array ( ArrayNode node ) {

			int n;

			GenValues ( node, out n );

			Instr instr = null;

			if ( n >= 0 ) {
				Pop ( n );
				AppendInstruction ( GenOp_2 ( OpCode.Array, vm.CurrentScope.sp, n ) );
				Push ();
			}

			return instr;
		}

		public Instr Emit_Hash ( HashNode node ) {

			int len = 0;

			for ( var i = 0; i < node.keys.Count; ++i ) {
				node.keys[ i ].Compile ( this );
				node.values[ i ].Compile ( this );
				len++;
			}

			Instr instr = null;

			if ( len >= 0 ) {
				Pop ( len * 2 );
				AppendInstruction ( GenOp_2 ( OpCode.Hash, vm.CurrentScope.sp, len ) );
				Push ();
			}

			return instr;
		}

		public Instr Emit_Asgn ( AsgnNode node ) {
			
			// 先编译右值
			node.cdr.Compile ( this );
			
			// 然后把堆栈中的值赋给左值
			return GenAssignment ( node.car, vm.CurrentScope.sp );
		}
		
		public Instr Emit_RangeInc ( Dot2Node node ) {

			node.car.Compile ( this );
			node.cdr.Compile ( this );

			Pop ( 2 );
			
			var instr = AppendInstruction ( new Instr ( OpCode.RangeInc, vm.CurrentScope.sp ) );

			Push ();
			
			return instr;
		}
		
		public Instr Emit_RangeExc ( Dot3Node node ) {

			node.car.Compile ( this );
			node.cdr.Compile ( this );

			Pop ( 2 );
			
			var instr = AppendInstruction ( new Instr ( OpCode.RangeExc, vm.CurrentScope.sp ) );

			Push ();
			
			return instr;
		}

		public Instr Emit_Class ( ClassNode node ) {

			// 先编译继承类(可选)
			node.super?.Compile ( this );

			// 在编译类名
			string name = ( (INamedNode)node.name ).name;
			GenOp_2 ( OpCode.Class, vm.CurrentScope.sp, vm.Symbol ( name ) );

			// 然后编译类主体
			var idx = GenScopeBody ( vm.CurrentScope, node.body );
			var instr = GenOp_2 ( OpCode.EXEC, vm.CurrentScope.sp, idx );

			Push ();

			return instr;
		}

		#region Util
		internal static bool no_peephole ( Scope s ) {
			return s.lastlabel == s.pc || s.pc == 0 || s.pc == s.lastpc;
		}
		#endregion
	}
}
