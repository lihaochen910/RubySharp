using System.Globalization;


namespace RubySharp.Core {
	
	using System;
	using System.IO;
	using System.Collections.Generic;
	using RubySharp.Core.Compiler;

	/// <summary>
	/// 代码生成作用域
	/// </summary>
	public class CodeGenScope {

		public const int MAX_STACK_SIZE  = 0xffff;
		public const int MAX_INSTR_SIZE  = 1 << 16;
		public const int MAX_RLEV_LENGTH = 1024;
		
		public VM vm;
		public CodeGenScope prev;
		public AstNode lv;

		public int sp;	// 内部栈指针，指向栈顶位置
		public int pc;	// 当前指令的位置
		public int lastpc;
		public int lastlabel;
		
		public int ensureLevel;
		public string filename;
		public int lineno;

		public AstParserState parser;
		public List<Instr>    iseq = new List < Instr >();
		public IRep           irep;

		public int rlev;  // 递归深度

		public Instr Last {
			get {
				if ( pc == lastpc ) {
					return new Instr ( OpCode.Nop );
				}

				return iseq[ lastpc ];
			}
		}
		
		public CodeGenScope PushOP ( Instr instr ) {

			if ( pc >= MAX_INSTR_SIZE ) {
				throw new Exception ( "too big code block" );
			}
			
			lastpc = pc;
			
			iseq.Add ( instr );

			pc++;
			
			return this;
		}

		public int FindLocalVarIdx ( string id, bool findParent = false ) {
			var current = this;
			int idx = current.irep.GetSym ( id );
			if ( !findParent ) {
				return idx;
			}
			
			current = current.prev;
			
			while ( current != null ) {
				
				idx = current.irep.GetSym ( id );
				
				if ( idx == -1 ) {
					current = current.prev;
				}
				else {
					return idx;
				}
			}

			return idx;
		}

		public void DebugTravel () {
			foreach ( var instr in iseq ) {
				Console.WriteLine ( instr );
			}
		}
	}

	/// <summary>
	/// 中间码生成
	/// </summary>
	public class CodeGen {

		public const int NOVAL        = 0;
		public const int VAL          = 1;
		public const int CALL_MAXARGS = 127;

		private VM state;

		public CodeGen ( VM state ) {
			this.state = state;
		}
		
		public CodeGenScope GenerateCode ( AstParserState p, int val ) {
			CodeGenScope scope = NewScope ( null, p );
			AddScopeIrep ( scope, new IRep() );

			
			
			Gen ( scope, p.tree, NOVAL );
			return scope;
		}

		private void Gen ( CodeGenScope s, AstNode tree, int val ) {
			int nt;
			int rlev = s.rlev;

			if ( tree == null ) {
				if ( val != NOVAL ) {
					GenOp_1 ( s, OpCode.LoadNIL, s.sp );
					Push ( s, 1 );
				}
				return;
			}
			
			s.rlev++;
			if ( s.rlev > CodeGenScope.MAX_RLEV_LENGTH ) {
				throw new Exception ( "too complex expression" );
			}
			
			s.lineno = tree.lineno;
			
			Console.WriteLine ( $"Gen: {tree}" );

			switch ( tree.type ) {
				case AstNodeType.LAMBDA:
					if ( val != NOVAL ) {
						int idx = LambdaBody ( s, tree, true ) ;
						GenOp_2 ( s, OpCode.Lambda, s.sp, idx );
						Push ( s );
					}
					break;
				case AstNodeType.BLOCK:
					if ( val != NOVAL ) {
						int idx = LambdaBody ( s, tree, true ) ;
						GenOp_2 ( s, OpCode.Block, s.sp, idx );
						Push ( s );
					}
					break;
				case AstNodeType.IF:
					int pos1, pos2;
					IfNode ifNode = tree.As< IfNode > ();
					AstNode elsepart = ifNode.@else;

					if ( ifNode.cond == null ) {
						Gen ( s, elsepart, val );
						goto exit;
					}

					switch ( ifNode.cond.type ) {
						case AstNodeType.TRUE:
						case AstNodeType.INT:
						case AstNodeType.STR:
							Gen ( s, ifNode.then, val );
							goto exit;
						case AstNodeType.FALSE:
						case AstNodeType.NIL:
							Gen ( s, elsepart, val );
							goto exit;
					}
					
					Gen ( s, ifNode.cond, val );
					Pop ( s, 1 );

					pos1 = GenJmp2 ( s, OpCode.JmpNot, s.sp, 0, val );
					Gen ( s, ifNode.then, val );

					if ( elsepart != null ) {
						if ( val != NOVAL ) Pop ( s, 1 );
						pos2 = GenJmp ( s, OpCode.Jmp, 0 );
						Gen ( s, elsepart, val );
					}
					else {
						if ( val != NOVAL ) {
							Pop ( s, 1 );
							pos2 = GenJmp ( s, OpCode.Jmp, 0 );
							GenOp_1 ( s, OpCode.LoadNIL, s.sp );
							Push ( s, 1 );
						}
						else {
							//dispatch ( s, pos1 );
						}
					}

					break;
				case AstNodeType.AND:
				case AstNodeType.OR:
				case AstNodeType.WHILE:
				case AstNodeType.UNTIL:
				case AstNodeType.FOR:
					ForBody ( s, tree );
					if ( val != NOVAL ) Push ( s );
					break;
				case AstNodeType.CASE:
					// TODO:
					break;
				case AstNodeType.SCOPE:
					ScopeBody ( s, tree, NOVAL );
					break;

				case AstNodeType.FCALL:
				case AstNodeType.CALL:
					GenCall ( s, tree, 0, val, false );
					break;
				case AstNodeType.SCALL:
					GenCall ( s, tree, 0, 0, true );
					break;
				case AstNodeType.DOT2:
					Dot2Node dot2Node = tree.As< Dot2Node > ();
					Gen ( s, dot2Node.car, val );
					Gen ( s, dot2Node.cdr, val );
					if ( val != NOVAL ) {
						Pop ( s ); Pop ( s );
						GenOp_1 ( s, OpCode.RangeInc, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.DOT3:
					Dot3Node dot3Node = tree.As< Dot3Node > ();
					Gen ( s, dot3Node.car, val );
					Gen ( s, dot3Node.cdr, val );
					if ( val != NOVAL ) {
						Pop ( s ); Pop ( s );
						GenOp_1 ( s, OpCode.RangeExc, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.COLON2:
				case AstNodeType.COLON3:
				case AstNodeType.ARRAY:
				case AstNodeType.HASH:
				case AstNodeType.KW_HASH:
					// TODO:
					break;
				case AstNodeType.SPLAT:
					// TODO:
					break;
				case AstNodeType.ASGN:
					GenAssignment ( s, tree, s.sp, val );
					break;
				case AstNodeType.MASGN:
				case AstNodeType.OP_ASGN:
				case AstNodeType.SUPER:
				case AstNodeType.RETURN:
				case AstNodeType.YIELD:
				case AstNodeType.BREAK:
				case AstNodeType.NEXT:
				case AstNodeType.REDO:
				case AstNodeType.RETRY:
					// TODO:
					break;
				case AstNodeType.LVAR:
					if ( val != NOVAL ) {
						int idx = s.FindLocalVarIdx ( nsym ( tree ) );

						if ( idx > 0 ) {
							GenMove ( s, s.sp, idx, val != 0 );
							if ( val != 0 && s.parser.on_eval ) GenOp_0 ( s, OpCode.Nop );
						}
						else {
							int          lv = 0;
							CodeGenScope up = s.prev;

							while ( up != null ) {
								idx = up.FindLocalVarIdx ( nsym ( tree ) );
								if ( idx > 0 ) {
									GenOp_3 ( s, OpCode.GetUpVar, s.sp, idx, lv );
									break;
								}

								lv++;
								up = up.prev;
							}
						}

						Push ( s );
					}
					break;
				case AstNodeType.GVAR:
					int gsym = NewSym ( s, nsym ( tree ) );
					GenOp_2 ( s, OpCode.GetGV, s.sp, gsym );
					if ( val!= 0 ) Push ( s );
					break;
				case AstNodeType.IVAR:
					int isym = NewSym ( s, nsym ( tree ) );
					GenOp_2 ( s, OpCode.GetIV, s.sp, isym );
					if ( val!= 0 ) Push ( s );
					break;
				case AstNodeType.CVAR:
					int csym = NewSym ( s, nsym ( tree ) );
					GenOp_2 ( s, OpCode.GetCV, s.sp, csym );
					if ( val != NOVAL ) Push ( s );
					break;
				case AstNodeType.CONST:
					int cnstSym = NewSym ( s, nsym ( tree ) );
					GenOp_2 ( s, OpCode.GetConst, s.sp, cnstSym );
					if ( val != NOVAL ) Push ( s );
					break;
				case AstNodeType.DEFINED:
					Gen (s, tree, val );
					break;
				case AstNodeType.ARG:
					/* should not happen */
					break;
				case AstNodeType.BLOCK_ARG:
					Gen ( s, tree, val );
					break;
				case AstNodeType.INT:
					if ( val != NOVAL ) {
						int off = NewLiteral ( s, tree.As<IntNode>().value );
						GenOp_2 ( s, OpCode.LoadI, s.sp, off );
						
						Push ( s );
					}
					break;
				case AstNodeType.FLOAT:
					if ( val != NOVAL ) {
						int off = NewLiteral ( s, tree.As<FloatNode>().value );
						GenOp_2 ( s, OpCode.LoadI, s.sp, off );
						
						Push ( s );
					}
					break;
				case AstNodeType.STR:
					if ( val != NOVAL ) {
						int off = NewLiteral ( s, Value.Str ( tree.As<StringNode>().token ) );
						GenOp_2 ( s, OpCode.String, s.sp, off );
						
						Push ( s );
					}
					break;
				case AstNodeType.SYM:
					if ( val != NOVAL ) {
						int off = NewSym ( s, nsym ( tree ) );
						GenOp_2 ( s, OpCode.LoadSYM, s.sp, off );
						
						Push ( s );
					}
					break;
				case AstNodeType.SELF:
					if ( val != NOVAL ) {
						GenOp_1 ( s, OpCode.LoadSelf, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.NIL:
					if ( val != NOVAL ) {
						GenOp_1 ( s, OpCode.LoadNIL, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.TRUE:
					if ( val != NOVAL ) {
						GenOp_1 ( s, OpCode.LoadT, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.FALSE:
					if ( val != NOVAL ) {
						GenOp_1 ( s, OpCode.LoadF, s.sp );
						Push ( s );
					}
					break;
				case AstNodeType.CLASS:
				case AstNodeType.MODULE:
				case AstNodeType.SCLASS:
				case AstNodeType.DEF:
				case AstNodeType.SDEF:
					// TODO:
					break;
				case AstNodeType.CMD_LIST:
					foreach ( var cmd in tree.As < ExpressionListNode > ().exprs ) {
						Gen ( s, cmd, val );
					}
					break;
				default:
					break;
			}
			
exit:
			s.rlev = rlev;
		}
		
		private void GenOp_0 ( CodeGenScope s, OpCode op ) {
			s.PushOP ( new Instr ( op ) );
		}

		private void GenOp_1 ( CodeGenScope s, OpCode op, int a ) {
			s.PushOP ( new Instr ( op, a ) );
		}
		
		private void GenOp_2 ( CodeGenScope s, OpCode op, int a, int b ) {
			s.PushOP ( new Instr ( op, a, b ) );
		}
		
		private void GenOp_3 ( CodeGenScope s, OpCode op, int a, int b, int c ) {
			s.PushOP ( new Instr ( op, a, b, c ) );
		}
		
		private int GenJmp ( CodeGenScope scope, OpCode op, int pc ) {
			int pos;
			
			scope.lastpc = scope.pc;
			scope.PushOP ( new Instr ( op, pc ) );
			pos = scope.pc;
			
			return pos;
		}

		private int GenJmp2 ( CodeGenScope scope, OpCode op, int a, int pc, int val ) {
			int pos;

			if ( val != NOVAL ) {
				Instr instr = scope.Last;
				if ( instr.insn == OpCode.Move && instr.a == a ) {
					scope.pc = scope.lastpc;
					a = instr.b;
				}
			}

			scope.lastpc = scope.pc;
			scope.PushOP ( new Instr ( op, a, pc ) );
			pos = scope.pc;
			
			return pos;
		}

		private void GenMove ( CodeGenScope s, int dst, int src, bool nopeep ) {
			
			if ( no_peephole ( s ) ) {
				goto normal;
			}
			else {
				Instr data = s.Last;
				switch ( data.insn ) {
					case OpCode.Move:
						if ( dst == src ) return;             /* remove useless MOVE */
						if ( data.b == dst && data.a == src ) /* skip swapping MOVE */
							return;
						goto normal;
					case OpCode.LoadNIL:
					case OpCode.LoadSelf:
						if ( nopeep || data.a != src /*|| data.a < s->nlocals*/ ) goto normal;
						s.pc = s.lastpc;
						GenOp_1 ( s, data.insn, dst );
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
						GenOp_2 ( s, data.insn, dst, data.b );
						break;
					default:
						goto normal;
				}
			}
			return;
			
normal:
			s.PushOP ( new Instr ( OpCode.Move, dst, src ) );
				
			// TODO: on_eval
			return;
		}
		
		private void GenReturn ( CodeGenScope s, OpCode op, int src ) {
			
			if ( no_peephole ( s ) ) {
				GenOp_1 ( s, op, src );
			}
			else {
				Instr data = s.Last;
				if ( data.insn == OpCode.Move && src == data.a ) {
					s.pc = s.lastpc;
					GenOp_1 ( s, op, data.b );
				}
				else if ( data.insn != OpCode.Return ) {
					GenOp_1 ( s, op, src );
				}
			}
		}

		private void GenAddOrSub ( CodeGenScope s, OpCode op, int dst ) {
			
//			if ( no_peephole ( s ) ) {
//				goto normal;
//			}
//			else {
//				Instr data = s.Last;
//				switch ( data.insn ) {
//					case OpCode.LoadI:
//						
//				}
//			}
//			
//			return;

normal:
			GenOp_1 ( s, op, dst );
		}

		private int GenValues ( CodeGenScope s, AstNode t, int val, int extra ) {
			
			int n = 0;
			// TODO: support splat mode
//			bool is_splat;
			if ( t is ArgsNode ) {
				ArgsNode argsNode = t.As< ArgsNode > ();
				foreach ( var arg in argsNode.args ) {
					Gen ( s, arg, val );
					n++;
				}
			}

			if ( t is ExpressionListNode ) {
				ExpressionListNode exprList = t.As< ExpressionListNode > ();
				foreach ( var expr in exprList.exprs ) {
					Gen ( s, expr, val );
					n++;
				}
			}

			return n;
		}

		private void GenCall ( CodeGenScope s, AstNode tree, int sp, int val, bool safe ) {
			int skip = 0;
			int n    = 0, noop = 0, sendv = 0, blk = 0;

			CallNode callNode = tree.As< CallNode > ();
			Gen ( s, callNode.car, val );

			if ( safe ) {
				int recv = s.sp - 1;
				GenMove ( s, s.sp, recv, true );
				skip = GenJmp2 ( s, OpCode.JmpNil, s.sp, 0, val );
			}

			if ( callNode.cdr ) {
				n = GenValues ( s, callNode.cdr, VAL, sp == 1 ? 1 : 0 );
				if (n < 0) {
					n = noop = sendv = 1;
					Push ( s, 1 );
				}
			}

			if ( sp != 0 ) { /* last argument pushed (attr=) */
				
			}

//			if ( callNode.cdr &&
//			     ( callNode.cdr as ArgsNode ).block != null ) {
//				noop = 1;
//				Gen ( s, ( callNode.cdr as ArgsNode ).block, VAL );
//				Pop ( s, 1 );
//				blk = 1;
//			}
			
			Push ( s, 1 ); Pop ( s, 1 );
			Pop ( s, n+1 );

			SymNode sym = callNode.fname.As< SymNode > ();
			int symlen = sym.name.Length;
			bool bnoop = noop == 1;

			if ( !bnoop && symlen == 1 && sym.name == "+" && n == 1 ) {
				GenAddOrSub ( s, OpCode.Add, s.sp );
			}
			else if ( !bnoop && symlen == 1 && sym.name == "-" && n == 1 ) {
				GenOp_1 ( s, OpCode.Sub, s.sp );
			}
			else if ( !bnoop && symlen == 1 && sym.name == "*" && n == 1 ) {
				GenOp_1 ( s, OpCode.Mul, s.sp );
			}
			else if ( !bnoop && symlen == 1 && sym.name == "/" && n == 1 ) {
				GenOp_1 ( s, OpCode.Div, s.sp );
			}
			else if ( !bnoop && symlen == 1 && sym.name == "<" && n == 1 ) {
				GenOp_1 ( s, OpCode.LT, s.sp );
			}
			else if ( !bnoop && symlen == 2 && sym.name == "<=" && n == 1 ) {
				GenOp_1 ( s, OpCode.LE, s.sp );
			}
			else if ( !bnoop && symlen == 1 && sym.name == ">" && n == 1 ) {
				GenOp_1 ( s, OpCode.GT, s.sp );
			}
			else if ( !bnoop && symlen == 2 && sym.name == ">=" && n == 1 ) {
				GenOp_1 ( s, OpCode.GE, s.sp );
			}
			else if ( !bnoop && symlen == 2 && sym.name == "==" && n == 1 ) {
				GenOp_1 ( s, OpCode.EQ, s.sp );
			}
			else {
				int idx = NewSym ( s, sym.name );

				if ( sendv != 0 ) {
					GenOp_2 ( s, blk != 0 ? OpCode.SendVB : OpCode.SendV, s.sp, idx );
				}
				else {
					GenOp_3 ( s, blk != 0 ? OpCode.SendB : OpCode.Send, s.sp, idx, n );
				}
			}

			if ( safe ) {
				
			}

			if ( val != NOVAL ) {
				Push ( s, 1 );
			}
		}

		private void GenAssignment ( CodeGenScope s, AstNode tree, int sp, int val ) {
			
			int      idx;
			AsgnNode asgnNode = tree.As< AsgnNode > ();
			
			Gen ( s, asgnNode.cdr, VAL );
			Pop ( s );
			
			switch ( asgnNode.car.type ) {
				case AstNodeType.GVAR:
					idx = NewSym ( s, asgnNode.car.As<GlobalVarNode>().name );
					GenOp_2 ( s, OpCode.SetGV, sp, idx );
					break;
				case AstNodeType.ARG:
				case AstNodeType.LVAR:
					idx = s.FindLocalVarIdx ( asgnNode.car.As<SymNode> ().name );
					if ( idx != -1 ) {
						if ( idx != sp ) {
							GenMove ( s, idx, sp, val == 1 );
							if ( val == 1 && s.parser.on_eval ) {
								GenOp_0 ( s, OpCode.Nop );
							}
						}
						break;
					}
					else {
						int lv = 0;
						CodeGenScope up = s.prev;

						while ( up != null ) {
							idx = up.FindLocalVarIdx ( asgnNode.car.As< SymNode > ().name );
							if ( idx != -1 ) {
								GenOp_3(s, OpCode.SetUpVar, sp, idx, lv);
								break;
							}

							lv++;
							up = up.prev;
						}
					}
					break;
				case AstNodeType.IVAR:
					idx = NewSym ( s, nsym ( asgnNode.car ) );
					GenOp_2 ( s, OpCode.SetIV, sp, idx );
					break;
				case AstNodeType.CVAR:
					idx = NewSym ( s, nsym ( asgnNode.car ) );
					GenOp_2 ( s, OpCode.SetCV, sp, idx );
					break;
				case AstNodeType.CONST:
					idx = NewSym ( s, nsym ( asgnNode.car ) );
					GenOp_2 ( s, OpCode.SetConst, sp, idx );
					break;
				case AstNodeType.COLON2:
					// TODO:
					break;
				case AstNodeType.CALL:
				case AstNodeType.SCALL:
					Push ( s );
					GenCall ( s, asgnNode.car, sp, NOVAL, asgnNode.car.type == AstNodeType.SCALL );
					Pop ( s );
					if ( val != NOVAL ) {
						GenMove ( s, s.sp, sp, false );
					}
					break;
				case AstNodeType.MASGN:
					// TODO:
					break;
				case AstNodeType.NIL:
					break;
					
				default:
					Console.WriteLine ( $"unknown lhs {asgnNode.car.type}\n" );
					break;
			}

			if ( val != NOVAL ) {
				Push ( s );
			}
		}

		private void GenIntern ( CodeGenScope s ) {
			Pop ( s, 1 );
			GenOp_1 ( s, OpCode.Intern, s.sp );
			Push ( s, 1 );
		}

		private void RaiseError ( CodeGenScope s, string msg ) {
			int idx = NewLiteral ( s, Value.Str ( msg ) );

			GenOp_1 ( s, OpCode.Err, idx );
		}

		private void GenRetVal ( CodeGenScope s, AstNode tree ) {
			if ( tree.type == AstNodeType.SPLAT ) {
				// TODO:
			}
			else {
				Gen ( s, tree, VAL );
				Pop ( s );
			}
		}

		private void ScopeBody ( CodeGenScope s, AstNode tree, int val ) {
			
			CodeGenScope scope = NewScope ( s, s.parser );

			switch ( tree.type ) {
				case AstNodeType.CLASS:
				case AstNodeType.SCLASS:
					Gen ( scope, tree.As< ClassNode > ().body, val );
					break;
				case AstNodeType.MODULE:
					Gen ( scope, tree.As< ModuleNode > ().body, val );
					break;
			}
		}
		
		private int NewLiteral ( CodeGenScope s, Value value ) {
			return s.irep.AddNewValue ( value );
		}

		private int NewSym ( CodeGenScope s, string sym ) {
			return s.irep.GetOrAddSym ( sym );
		}

		private int AttrSym ( CodeGenScope s, string sym ) {
			return NewSym ( s, sym + "=" );
		}
		
		private void ForBody ( CodeGenScope s, AstNode tree ) {
			CodeGenScope prev = s;
			int idx;

			// TODO:
		}

		private int LambdaBody ( CodeGenScope s, AstNode tree, bool blk ) {
			CodeGenScope parent = s;
			s = NewScope ( parent, parent.parser );

			// TODO:
			return -1;
		}

		/// <summary>
		/// 作用域栈顶指针增加
		/// </summary>
		/// <param name="scope"></param>
		/// <param name="n"></param>
		/// <exception cref="Exception"></exception>
		private void Push ( CodeGenScope scope, int n = 1 ) {
			
			if ( scope.sp + n >= CodeGenScope.MAX_STACK_SIZE ) {
				throw new Exception ( "too complex expression" );
			}

			scope.sp += n;
		}

		private void Pop ( CodeGenScope scope, int n = 1 ) {
			
			if ( scope.sp - n < 0 ) {
//				throw new Exception ( $"stack pointer underflow {scope.sp} - {n}" );
				Console.WriteLine ( $"stack pointer underflow {scope.sp} - {n}" );
			}

			scope.sp -= n;
		}
		
		
		public CodeGenScope NewScope ( CodeGenScope parent, AstParserState parser ) {
			CodeGenScope scope = new CodeGenScope ();
			scope.vm     = state;
			scope.parser = parser;

			if ( parent == null ) {
				return scope;
			}

			scope.prev = parent;
			
			AddScopeIrep ( scope, new IRep () );
			
//			scope.sp = 

			scope.rlev = parent.rlev + 1;
			
			return scope;
		}

		public void AddScopeIrep ( CodeGenScope s, IRep irep ) {
			
			if ( s.irep == null ) {
				s.irep = irep;
			}
			
			s.irep.reps.Add ( irep );
		}

		public void FinishScope ( CodeGenScope scope ) {
			
		}

		private int lv_idx ( CodeGenScope s, string name ) {
			return s.FindLocalVarIdx ( name );
		}

		private string nsym ( AstNode tree ) {
			
			switch ( tree.type ) {
				case AstNodeType.SYM:
//					return tree.As< SymNode > ().name;
				case AstNodeType.GVAR:
//					return tree.As< GlobalVarNode > ().name;
				case AstNodeType.IVAR:
//					return tree.As< InstanceVarNode > ().name;
				case AstNodeType.CVAR:
//					return tree.As< ClassVarNode > ().name;
				case AstNodeType.LVAR:
//					return tree.As< LocalVarNode > ().name;
				case AstNodeType.CONST:
//					return tree.As< ConstantNode > ().name;
					return ( ( INamedNode )tree ).name;
			}

			return string.Empty;
		}

		private bool no_peephole ( CodeGenScope s ) {
			return s.lastlabel == s.pc || s.pc == 0 || s.pc == s.lastpc;
		}
	}

} // end namespace
