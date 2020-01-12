namespace RubySharp.Core {
	
	using System;
	using System.IO;
	using System.Collections.Generic;
	
	
	public delegate Value RubyFunction ( Value self, RubyContext context, IList< Value > values );
	
	
	public interface IFunction {
		Value Invoke ( Value self, RubyContext context, IList< Value > values );
	}
	
	
	public class DefinedFunction : IFunction {
		private ExpressionListNode body;
		private ArgsNode           parameters;
		private RubyContext        context;
		internal bool              isConstructor;

		public DefinedFunction ( ExpressionListNode body, ArgsNode parameters, RubyContext context ) {
			this.body       = body;
			this.context    = context;
			this.parameters = parameters;
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			RubyContext newContext = context.VM.NewContext ( context.VM.rb_obj_ptr ( self ), this.context );

			int k  = 0;
			// int cv = values.Count;

			foreach ( var parameter in parameters.args ) {
				
				switch ( parameter.type ) {
					case AstNodeType.CONST:
					case AstNodeType.LVAR:
					case AstNodeType.SYM:
						newContext.SetLocalValue ( 
							( parameter as INamedNode ).name,
							context.VM.GetArg< Value > ( values, k ) 
						);
						k++;
						break;
					default:
						Console.WriteLine ( $"DefinedFunction::Invoke param type: {parameter.type} 未实现" );
						break;
				}
			}

			// if ( isConstructor ) {
			// 	body.Evaluate ( newContext );
			// 	return self;
			// }

			return body.Evaluate ( newContext );
		}
	}


	public class LambdaFunction : IFunction {
		private Func< Value, RubyContext, IList< Value >, Value > lambda;

		public LambdaFunction ( Func< Value, RubyContext, IList< Value >, Value > lambda ) {
			this.lambda = lambda;
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			return lambda ( self, context, values );
		}
	}
	
	
	public class BlockFunction : IFunction {
		
		private ArgsNode           argumentNames;
		private ExpressionListNode body;
		private RubyContext        context;

		public BlockFunction ( ArgsNode argumentNames, ExpressionListNode body, RubyContext context ) {
			this.argumentNames = argumentNames;
			this.body          = body;
			this.context       = context;
		}
		
		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			
			if ( argumentNames == null || argumentNames.args.Count == 0 ) {
				return body.Evaluate ( this.context );
			}
			
			RubyBlockContext blockContext = context.VM.NewBlockContext ( context.Self, context );

			for ( int k = 0; k < argumentNames.args.Count; k++ ) {
				if ( values != null && k < values.Count ) {
					blockContext.SetLocalValue ( ( ( INamedNode )argumentNames.args[ k ] ).name, values[ k ] );
				}
				else {
					blockContext.SetLocalValue ( ( ( INamedNode )argumentNames.args[ k ] ).name, Value.Nil () );
				}
			}
			
			return body.Evaluate ( blockContext );
		}
	}

	
	public class PutsFunction : IFunction {
		private TextWriter writer;
		
		public PutsFunction ( TextWriter writer ) {
			this.writer = writer;
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			
			foreach ( var value in values ) {
				writer.WriteLine ( context.VM.ValueToString ( value ) );
			}
			
			return null;
		}
	}

	
	public class PrintFunction : IFunction {
		private TextWriter writer;

		public PrintFunction ( TextWriter writer ) {
			this.writer = writer;
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			
			foreach ( var value in values ) {
				this.writer.WriteLine ( value );
			}

			return null;
		}
	}
	
	
	public class RandFunction : IFunction {
		private Random random;
		
		public RandFunction () {
			this.random = new Random ();
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {

			Value val = context.VM.GetArg< Value > ( values, 0 );

			if ( val.type == ValueType.Fixnum ) {
				return Value.Fixnum ( random.Next ( val.i ) );
			}
			
			if ( val.type == ValueType.Float ) {
				return Value.Fixnum ( random.Next ( ( int )val.f ) );
			}
			
			if ( val.type == ValueType.Range ) {
				var range = val.As< Range > ();
				return Value.Fixnum ( random.Next ( range.@from, range.to ) );
			}

			return val;
		}
	}


	public class RequireFunction : IFunction {
		private VM vm;

		public RequireFunction ( VM vm ) {
			this.vm = vm;
		}

		public Value Invoke ( Value self, RubyContext context, IList< Value > values ) {
			string filename = vm.GetArg< string > ( values, 0 );
			return Value.Bool ( vm.RequireFile ( filename ) );
		}
	}
}
