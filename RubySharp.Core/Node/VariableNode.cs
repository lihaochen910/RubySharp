using System;
using System.Collections.Generic;
using RubySharp.Core.Language;


namespace RubySharp.Core {
	
	public class LocalVarNode : AstNode, INamedNode {
		
		private static IList< Value > EmptyValues = new Value[] {};
		
		public override AstNodeType type => AstNodeType.LVAR;
		public string name { get; set; }

		public LocalVarNode ( string name ) {
			this.name = name;
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_LocalVar ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			if ( context.HasLocalValue ( this.name ) )
				return context.GetLocalValue ( this.name );

			if ( context.Self != null ) {
				var method = context.Self.GetMethod ( name );
				
				if ( method != null ) {
					return method.Invoke ( context.VM.rb_obj_value ( context.Self ), context, EmptyValues );
				}
			}

			return Value.Nil ();
			// throw new Exception ( string.Format ( "unitialized localvar {0}", name ) );
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class InstanceVarNode : AstNode, INamedNode {
		public override AstNodeType type => AstNodeType.IVAR;
		public string name { get; set; }

		public InstanceVarNode ( string name ) {
			this.name = name;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_InstanceVar ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			return context.Self.GetIV ( name );
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class GlobalVarNode : AstNode, INamedNode {
		public override AstNodeType type => AstNodeType.GVAR;
		public string name { get; set; }

		public GlobalVarNode ( string name ) {
			this.name = name;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_GlobalVar ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			if ( context.HasValue ( this.name ) )
				return context.GetValue ( this.name );

			throw new Exception ( string.Format ( "unitialized constant '{0}'", name ) );
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class ClassVarNode : AstNode, INamedNode {
		public override AstNodeType type => AstNodeType.CVAR;
		public string name { get; set; }

		public ClassVarNode ( string name ) {
			this.name = name;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_ClassVar ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return context.Self.@class.GetIV ( name );
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class ConstantNode : AstNode, INamedNode {
		
		private static IList< Value > EmptyValues = new Value[] {};
		public override AstNodeType type => AstNodeType.CONST;
		public string name { get; set; }

		public ConstantNode ( string name ) {
			this.name = name;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_ConstVar ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			if ( context.HasValue ( name ) ) {
				return context.GetValue ( name );
			}

			if ( context.Self != null && context.Self is RClass ) {

				var method = ( ( RClass )context.Self ).GetInstanceMethod ( name );
				if ( method != null ) {
					return method.Invoke ( context.VM.rb_obj_value ( context.Self ), context, EmptyValues );
				}
			}

			throw new Exception ( string.Format ( "unitialized constant '{0}'", name ) );
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class SymbolNode : AstNode, INamedNode {
		public override AstNodeType type => AstNodeType.SYM;
		public string name { get; set; }
		public Value value;

		public SymbolNode ( string name ) {
			this.name = name;
			value = Value.Symbol ( new Symbol ( name ) );
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_Symbol ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}
		
		public override string ToString () {
			return $"{type}: {name}";
		}
	}
	
	public class SelfNode : AstNode {
		public override AstNodeType type => AstNodeType.SELF;
		public string token;

		public SelfNode ( string name ) {
			token = name;
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Self ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			if ( context.VM.IsCustomClass ( context.Self.Class ) ) {
				return Value.Data ( context.Self );
			}
			
			return Value.Object ( context.Self );
		}
	}
}
