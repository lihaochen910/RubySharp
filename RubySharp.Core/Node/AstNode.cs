using System;
using RubySharp.Core.Compiler;


namespace RubySharp.Core {
	
	using System.Collections.Generic;

	public interface INode {
		AstNodeType type { get; }
	}

	public interface INamedNode {
		string name { get; set; }
	}
	
	public interface ICompilable {
		void Compile ( ByteCode bc );
	}

	public interface IEvaluateable {
		Value Evaluate ( RubyContext context );
	}

	public abstract class AstNode : INode, ICompilable, IEvaluateable {
		public abstract AstNodeType type { get; }
		public string filename;
		public int lineno;

		public virtual void Compile ( ByteCode bc ) {}
		public virtual Value Evaluate ( RubyContext context ) { return null; }

		public T As< T > () where T : AstNode {
			return ( T )this;
		}

		public static implicit operator bool ( AstNode node ) {
			return node != null;
		}

		public override string ToString () {
			return $"{type} ({lineno})";
		}
	}
	
	public class ScopeNode : AstNode {
		public override AstNodeType type => AstNodeType.SCOPE;
		public AstNode vars;
		public AstNode prog;
	}

	public class ClassNode : AstNode {
		public override AstNodeType type => AstNodeType.CLASS;
		public AstNode name;
		public AstNode super;
		public ExpressionListNode body;

		public ClassNode ( AstNode name, AstNode super, ExpressionListNode body ) {
			this.name = name;
			this.super = super;
			this.body = body;
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Class ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			string name = AstParser.GetNamePathFinalName ( this.name );
			RClass target = ( context.Self is RClass ) ? ( RClass )context.Self : null;
			Value value = null;

			if ( context.Module != null ) {
				if ( context.Module.constants.HasLocalValue ( name ) ) {
					value = context.Module.constants.GetLocalValue ( name );
				}
			}
			else if ( context.HasValue ( name ) ) {
				value = context.GetValue ( name );
			}

			if ( value == null || !( value is RClass ) ) {
				var classclass = context.RootContext.GetLocalValue ( "Class" ).As< RClass > ();
				var superclass = context.RootContext.GetLocalValue ( "Object" ).As< RClass > ();
				var parent = target == null ? context.Module : target;

				if ( super != null ) {
					superclass = super.Evaluate ( context )?.As< RClass > ();
					if ( superclass == null ) {
						VM.ThrowException ( $"superclass '{super}' not found." );
					}
				}

				var newclass = context.VM.DefClass ( classclass, name, superclass, parent );
				value = Value.Class ( newclass );
				
				if ( context.VM.IsCustomClass ( superclass ) ) {
					context.VM.WriteCustomClassFlag ( newclass, context.VM.GetCustomClassType ( superclass ) );
					context.VM.WriteCustomClassRClass ( newclass, context.VM.GetCustomClassRClass ( superclass ) );
				}

				if ( parent == null ) {
					context.RootContext.SetLocalValue ( name, value );
				}
				else {
					parent.constants.SetLocalValue ( name, value );
				}
			}

			var dclass = value.As< RClass > ();

			RubyContext classcontext = context.VM.NewContext ( dclass, context );

			body.Evaluate ( classcontext );
			
			return null;
		}
	}

	public class ModuleNode : AstNode {
		public override AstNodeType type => AstNodeType.MODULE;
		public AstNode name;
		public AstNode body;

		public ModuleNode ( AstNode name, AstNode body ) {
			this.name = name;
			this.body = body;
		}
	}

	public class IfNode : AstNode {
		public override AstNodeType type => AstNodeType.IF;
		public AstNode cond;
		public AstNode then;
		public AstNode @else;

		public IfNode ( AstNode cond, AstNode then, AstNode @else ) {
			this.cond  = cond;
			this.then  = then;
			this.@else = @else;
		}

		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			Value value = cond.Evaluate ( context );

			if ( value == null || value.type == ValueType.False ) {
				if ( @else == null ) {
					return Value.Nil ();
				}
				else {
					return @else.Evaluate ( context );
				}
			}
			
			return then.Evaluate ( context );
		}
	}
	
	public class WhileNode : AstNode {
		public override AstNodeType type => AstNodeType.WHILE;
		public AstNode cond;
		public ExpressionListNode body;

		public WhileNode ( AstNode cond, ExpressionListNode body ) {
			this.cond = cond;
			this.body = body;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			for ( Value value = cond.Evaluate ( context ); value; value = cond.Evaluate ( context ) ) {
				body.Evaluate ( context );
			}

			return null;
		}
	}
	
	public class UntilNode : AstNode {
		public override AstNodeType type => AstNodeType.UNTIL;
		public AstNode cond;
		public AstNode body;

		public UntilNode ( AstNode cond, AstNode body ) {
			this.cond = cond;
			this.body = body;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			for ( Value value = cond.Evaluate ( context ); !value; value = cond.Evaluate ( context ) ) {
				body.Evaluate ( context );
			}

			return null;
		}
	}
	
	public class ForNode : AstNode {
		public override AstNodeType type => AstNodeType.FOR;
		public AstNode var;
		public AstNode obj;
		public ExpressionListNode body;

		private ArgsNode args;
		public ForNode ( AstNode var, AstNode obj, ExpressionListNode body ) {
			this.var  = var;
			this.obj  = obj;
			this.body = body;
			
			args = new ArgsNode ();
			args.Push ( var );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			var enumerable = obj.Evaluate ( context );
				
			var enumerableClass = context.VM.GetClass ( enumerable );
			var eachFunction = enumerableClass.GetInstanceMethod ( VM.EACH );

			if ( eachFunction == null ) {
				VM.ThrowException ( $"unimplemented method: '{VM.EACH}' for {enumerableClass} (line:{obj.lineno}) (NoMethodError)"  );
				return Value.Nil ();
			}
			
			return eachFunction.Invoke ( enumerable, context, new [] { Value.Proc ( new BlockFunction ( args, body, context ) ) } );
		}
	}
	
	public class DefNode : AstNode {
		public override AstNodeType type => AstNodeType.DEF;
		public AstNode  name;
		public ArgsNode args;
		public ExpressionListNode body;

		public DefNode ( AstNode name, ArgsNode args, ExpressionListNode body ) {
			this.name = name;
			this.args = args;
			this.body = body;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			var result = new DefinedFunction ( body, args, context );
			var fname = AstParser.GetNamePathFinalName ( name );
			if ( fname == VM.INITIALIZE ) {
				result.isConstructor = true;
			}

			if ( context.Module != null ) {
				context.Module.SetInstanceMethod ( fname, result );
			}
			else {
				// context.Self.c.SetInstanceMethod ( AstParser.GetNamePathFinalName ( name ), result );
				( ( RClass )context.Self ).SetInstanceMethod ( fname, result );
			}

			return null;
		}
		
	}
	
	public class SymNode : AstNode, INamedNode {
		public override AstNodeType type => AstNodeType.SYM;
		public string name { get; set; }

		public SymNode ( string name ) {
			this.name = name;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_Symbol ( this );
		}

		public override string ToString () {
			return $"{type}: {name}";
		}
	}

	public class DotNode : AstNode {
		public override AstNodeType type => AstNodeType.DOT;
		public AstNode car;
		public AstNode cdr;

		public DotNode ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}

		public override string ToString () {
			return $"{type}: {car} . {cdr}";
		}
	}

	public class Dot2Node : AstNode {
		public override AstNodeType type => AstNodeType.DOT2;
		public AstNode car;
		public AstNode cdr;

		public Dot2Node ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_RangeInc ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			var from = car.Evaluate ( context );
			var to = cdr.Evaluate ( context );

			if ( from.type == ValueType.Fixnum ||
			     from.type == ValueType.Float ) {
				if ( to.type == ValueType.Fixnum ||
				     to.type == ValueType.Float ) {
					return Value.Range ( new Range ( context.VM.rb_fixnum ( from ), context.VM.rb_fixnum ( to ) ) );
				}
			}
			
			if ( from.type == ValueType.String ) {
				if ( to.type == ValueType.String ) {
					// TODO:
					return Value.Range ( new Range ( context.VM.rb_fixnum ( from ), context.VM.rb_fixnum ( to ) ) );
				}
			}

			return Value.Nil ();
		}
	}

	public class Dot3Node : AstNode {
		public override AstNodeType type => AstNodeType.DOT3;
		public AstNode car;
		public AstNode cdr;

		public Dot3Node ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_RangeExc ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			var from = car.Evaluate ( context );
			var to   = cdr.Evaluate ( context );

			if ( from.type == ValueType.Fixnum ||
			     from.type == ValueType.Float ) {
				if ( to.type == ValueType.Fixnum ||
				     to.type == ValueType.Float ) {
					return Value.Range ( new Range ( context.VM.rb_fixnum ( from ), context.VM.rb_fixnum ( to ) - 1 ) );
				}
			}
			
			if ( from.type == ValueType.String ) {
				if ( to.type == ValueType.String ) {
					// TODO:
					return Value.Range ( new Range ( context.VM.rb_fixnum ( from ), context.VM.rb_fixnum ( to ) - 1 ) );
				}
			}

			return Value.Nil ();
		}
	}

	public class ArrayRefNode : AstNode {
		public override AstNodeType type => AstNodeType.AREF;
		public AstNode car;
		public AstNode cdr;

		public ArrayRefNode ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}

		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			var array = car.Evaluate ( context );
			var indexed = cdr.Evaluate ( context );

			return context.VM.FunctionCall ( array, "[]", new [] { indexed } );
		}
	}

	public class Colon2Node : AstNode {
		public override AstNodeType type => AstNodeType.COLON2;
		public AstNode car;
		public AstNode cdr;

		public Colon2Node ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			RClass cls = car.Evaluate ( context ).As< RClass > ();
			if ( cls != null ) {
				if ( cls.constants.HasLocalValue ( ( ( INamedNode )cdr ).name ) ) {
					return cls.constants.GetLocalValue ( ( ( INamedNode )cdr ).name );
				}
			}

			return null;
		}
	}

	public class AsgnNode : AstNode {
		public override AstNodeType type => AstNodeType.ASGN;
		public AstNode car;
		public AstNode cdr;

		public AsgnNode ( AstNode left, AstNode right ) {
			car = left;
			cdr = right;
		}
		
		public override void Compile ( ByteCode bc ) {
			bc.Emit_Asgn ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			Value ret = cdr.Evaluate ( context );
			
			switch ( car.type ) {
				case AstNodeType.GVAR:
					context.RootContext.SetLocalValue ( ( ( INamedNode )car ).name, ret );
					return ret;
				case AstNodeType.SYM:
				case AstNodeType.CONST:
				case AstNodeType.LVAR:
					context.SetLocalValue ( ( ( INamedNode )car ).name, ret );
					return ret;
				case AstNodeType.CVAR:
					context.Self.SingletonClass.SetIV ( ( ( INamedNode )car ).name, ret );
					return ret;
				case AstNodeType.IVAR:
					context.Self.SetIV ( ( ( INamedNode )car ).name, ret );
					return ret;
				case AstNodeType.AREF:
					var arrayRefNode = car.As< ArrayRefNode > ();
					var array = arrayRefNode.car.Evaluate ( context );
					var indexed = arrayRefNode.cdr.Evaluate ( context );
					context.VM.FunctionCall ( array, "[]=", new [] { indexed, ret } );
					break;
				case AstNodeType.DOT:
				default:
					VM.ThrowException ( $"AsgnNode type: {car.type} not impl." );
					break;
			}
			
			return ret;
		}

		public override string ToString () {
			return $"{type}: {car} = {cdr} ({lineno})";
		}
	}
	
	public class OpAsgnNode : AstNode {
		public override AstNodeType type => AstNodeType.OP_ASGN;
		public AstNode car;
		public AstNode cdr;
		public AstNode op;

		public OpAsgnNode ( AstNode left, string op, AstNode right ) {
			car = left;
			cdr = right;
			this.op = new SymNode ( op );
		}
	}
	
	public class CallNode : AstNode {
		public override AstNodeType type => AstNodeType.CALL;
		public AstNode car; // receiver
		public AstNode cdr; // args
		public AstNode fname;

		public CallNode ( AstNode left, AstNode fname, AstNode right ) {
			car = left;
			cdr = right;
			this.fname = fname;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			string name = ( ( INamedNode )fname ).name;

			IFunction function = null;
			Value left = null;

			if ( car != null ) {
				
				left = car.Evaluate ( context );
				
				var leftClass = context.VM.GetClass ( left );

				if ( left.type == ValueType.Class ||
				     left.type == ValueType.IClass ||
				     left.type == ValueType.SClass ) {
					switch ( left.type ) {
						case ValueType.Class:
						case ValueType.IClass:
						case ValueType.SClass:
							function = leftClass.GetClassMethod ( name );
							break;
					}
				}
				else {
#if DEBUG
					// TODO: standard class not impl
					if ( leftClass == null ) {
						VM.ThrowException ( $"{left} class not impl." );
					}
#endif
					function = leftClass.GetInstanceMethod ( name );
					
					// method_missing call
					if ( function == null ) {
						function = leftClass.GetInstanceMethod ( VM.METHOD_MISSING );
					}
				}
				
				// thow the exc
				if ( function == null ) {
					VM.ThrowException ( $"undefined method: '{name}' for {left.type} {fname}(line:{fname.lineno}) (NoMethodError)" );
				}
			}
			else {
				function = context.Self.GetMethod ( name );
				
				// find in top.
				if ( function == null ) {
					function = context.RootContext.Self.GetMethod ( name );
				}

				if ( function == null ) {
					VM.ThrowException ( $"no method found: {fname} in {car} Type {context.Self} \n{name}({fname.lineno})" );
				}
			}
			
			IList< Value > values = new List< Value > ();

			if ( cdr != null ) {
				
				if ( cdr is ArgsNode ) {
					foreach ( var argument in cdr.As< ArgsNode > ().args ) {
						values.Add ( argument.Evaluate ( context ) );
					}
				}
				else {
					values.Add ( cdr.Evaluate ( context ) );
				}
			} 
			
			return function.Invoke ( car != null ? left : context.VM.rb_obj_value ( context.Self ), context, values );
		}

		public override string ToString () {
			return $"{type}: (left){car} (fname){fname} (right){cdr}";
		}
	}

	public class SuperNode : AstNode {
		public override AstNodeType type => AstNodeType.SUPER;
		public AstNode args;

		/*
		public override Value Evaluate ( RubyContext context ) {

#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			string name = ( (INamedNode)fname ).name;

			IFunction function = null;
			Value left = null;

			if ( car != null ) {

				left = car.Evaluate ( context );

				var leftClass = context.VM.GetClass ( left );

				if ( left.type == ValueType.Class ||
					 left.type == ValueType.IClass ||
					 left.type == ValueType.SClass ) {
					switch ( left.type ) {
						case ValueType.Class:
						case ValueType.IClass:
						case ValueType.SClass:
							function = leftClass.GetClassMethod ( name );
							break;
					}
				}
				else {
#if DEBUG
					// TODO: standard class not impl
					if ( leftClass == null ) {
						VM.ThrowException ( $"{left} class not impl." );
					}
#endif
					function = leftClass.GetInstanceMethod ( name );

					// method_missing call
					if ( function == null ) {
						function = leftClass.GetInstanceMethod ( VM.METHOD_MISSING );
					}
				}

				// thow the exc
				if ( function == null ) {
					VM.ThrowException ( $"undefined method: '{name}' for {left.type} {fname}(line:{fname.lineno}) (NoMethodError)" );
				}
			}
			else {
				function = context.Self.GetMethod ( name );

				// find in top.
				if ( function == null ) {
					function = context.RootContext.Self.GetMethod ( name );
				}

				if ( function == null ) {
					VM.ThrowException ( $"no method found: {fname} in {car} Type {context.Self} \n{name}({fname.lineno})" );
				}
			}

			IList<Value> values = new List<Value> ();

			if ( cdr != null ) {

				if ( cdr is ArgsNode ) {
					foreach ( var argument in cdr.As<ArgsNode> ().args ) {
						values.Add ( argument.Evaluate ( context ) );
					}
				}
				else {
					values.Add ( cdr.Evaluate ( context ) );
				}
			}

			return function.Invoke ( car != null ? left : context.VM.rb_obj_value ( context.Self ), context, values );
		}
		*/
	}

	public class YieldNode : AstNode {
		public override AstNodeType type => AstNodeType.YIELD;
		public AstNode args;
	}

	public class ReturnNode : AstNode {
		public override AstNodeType type => AstNodeType.RETURN;
		public AstNode args;
	}

	public class BreakNode : AstNode {
		public override AstNodeType type => AstNodeType.BREAK;
	}

	public class NextNode : AstNode {
		public override AstNodeType type => AstNodeType.NEXT;
		public AstNode args;
	}

	public class RedoNode : AstNode {
		public override AstNodeType type => AstNodeType.REDO;
		public AstNode args;
	}

	public class RetryNode : AstNode {
		public override AstNodeType type => AstNodeType.RETRY;
		public AstNode args;
	}

	public class BlockNode : AstNode {
		public override AstNodeType type => AstNodeType.BLOCK;
		public ArgsNode car;
		public ExpressionListNode cdr;

		public BlockNode ( ArgsNode args, ExpressionListNode body ) {
			car = args;
			cdr = body;
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			return Value.Proc ( new BlockFunction ( car, cdr, context ) );
		}
	}
	
	public class ArgsNode : AstNode {
		public override AstNodeType type => AstNodeType.ARGS;
		public List<AstNode> args;
		public AstNode block;
		
		public ArgsNode () {
			args = new List < AstNode >();
		}
		
		public override void Compile ( ByteCode bc ) {
			foreach ( var arg in args ) {
				arg.Compile ( bc );
			}

			if ( block != null ) {
				block.Compile ( bc );
			}
		}

		public void Push ( AstNode arg ) {
			args.Add ( arg );
		}

		public override string ToString () {
			string argsContent = string.Empty;
			for ( var i = 0; i < args.Count; ++i ) {
				argsContent += args[ i ].ToString () + ( i == args.Count - 1 ? string.Empty : " " );
			}
			return $"{type}: {argsContent}";
		}
	}
	
	public class NamePathNode : AstNode {
		public override AstNodeType type => AstNodeType.LVAR;
		public List<AstNode> args;
		
		public NamePathNode () {
			args = new List < AstNode >();
		}

		public string Name {
			get {
				if ( args.Count > 0 ) {
					return (args[ args.Count - 1 ] as SymNode).name;
				}
				return string.Empty;
			}
		}

		public void Push ( AstNode arg ) {
			args.Add ( arg );
		}
		
		public override Value Evaluate ( RubyContext context ) {

			Value ret = null;

			foreach ( var arg in args ) {
				switch ( arg.type ) {
					case AstNodeType.SELF:
						
					case AstNodeType.SYM:
						// RArray array = car.Evaluate ( context );
						break;
				}
			}
			
			return null;
		}

		public override string ToString () {
			string argsContent = string.Empty;
			for ( var i = 0; i < args.Count; ++i ) {
				argsContent += args[i].ToString () + ( i == args.Count - 1 ? string.Empty : "::" );
			}
			return $"{type}: {argsContent}";
		}
	}

	public class ExpressionListNode : AstNode {
		public override AstNodeType type => AstNodeType.CMD_LIST;
		public List<AstNode> exprs;

		public ExpressionListNode () {
			exprs = new List<AstNode> ();
		}
		
		public override void Compile ( ByteCode bc ) {
			foreach ( var expr in exprs ) {
				expr.Compile ( bc );
			}
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif

			Value ret = null;
			
			foreach ( var expr in exprs ) {
				ret = expr.Evaluate ( context );
			}

			return ret;
		}

		public void Push ( AstNode arg ) {
			exprs.Add ( arg );
		}

		public override string ToString () {
			string argsContent = string.Empty;
			for ( var i = 0; i < exprs.Count; ++i ) {
				argsContent += exprs[i].ToString () + ( i == exprs.Count - 1 ? string.Empty : " " );
			}
			return $"{type}: {argsContent}";
		}
	}

	public enum AstNodeType {
		METHOD,
		SCOPE,
		BLOCK,
		IF,
		CASE,
		WHEN,
		WHILE,
		UNTIL,
		ITER,
		FOR,
		BREAK,
		NEXT,
		REDO,
		RETRY,
		BEGIN,
		RESCUE,
		ENSURE,
		AND,
		OR,
		NOT,
		MASGN,
		ASGN,
		CDECL,
		CVASGN,
		CVDECL,
		OP_ASGN,
		CALL,
		SCALL,
		FCALL,
		SUPER,
		ARRAY,
		HASH,
		KW_HASH,
		RETURN,
		YIELD,
		LVAR,
		DVAR,
		GVAR,
		IVAR,
		CONST,
		CVAR,
		NTH_REF,
		BACK_REF,
		MATCH,
		INT,
		FLOAT,
		NEGATE,
		LAMBDA,
		SYM,
		STR,
		DSTR,
		XSTR,
		DXSTR,
		REGX,
		DREGX,
		DREGX_ONCE,
		ARG,
		ARGS,
		ARGS_TAIL,
		KW_ARG,
		KW_REST_ARGS,
		SPLAT,
		TO_ARY,
		SVALUE,
		BLOCK_ARG,
		DEF,
		SDEF,
		ALIAS,
		UNDEF,
		CLASS,
		MODULE,
		SCLASS,
		COLON2,
		COLON3,
		DOT,
		DOT2,
		DOT3,
		AREF,
		SELF,
		NIL,
		TRUE,
		FALSE,
		DEFINED,
		POSTEXE,
		DSYM,
		LITERAL_DELIM,
		WORDS,
		SYMBOLS,
		LAST,
		CMD_LIST
	}
}
