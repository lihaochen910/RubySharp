using System.Collections.Generic;
using System.Globalization;


namespace RubySharp.Core {
	
	public class IntNode : AstNode {
		public override AstNodeType type => AstNodeType.INT;
		public string token;
		public Value  value;

		public IntNode ( string s, bool neg = false ) {
			token = s;
			value = Value.Fixnum ( neg ? -int.Parse ( s, CultureInfo.InvariantCulture ) : int.Parse ( s, CultureInfo.InvariantCulture ) );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}

		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}

		public override string ToString () {
			return $"{type}: {token}";
		}
	}
	
	public class FloatNode : AstNode {
		public override AstNodeType type => AstNodeType.FLOAT;
		public string token;
		public Value  value;

		public FloatNode ( string s, bool neg = false ) {
			token = s;
			value = Value.Float ( neg ? -float.Parse ( s, CultureInfo.InvariantCulture ) : float.Parse ( s, CultureInfo.InvariantCulture ) );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}

		public override string ToString () {
			return $"{type}: {token}";
		}
	}
	
	public class StringNode : AstNode {
		public override AstNodeType type => AstNodeType.STR;
		public string token;
		public Value  value;

		public StringNode ( string s ) {
			token = s;
			value = Value.Str ( s );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}

		public override string ToString () {
			return $"{type}: {token}";
		}
	}
	
	public class TrueNode : AstNode {
		public override AstNodeType type => AstNodeType.TRUE;
		public string token;
		public Value  value;

		public TrueNode ( string s ) {
			token = s;
			value = Value.Bool ( bool.Parse ( s ) );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}
	}
	
	public class FalseNode : AstNode {
		public override AstNodeType type => AstNodeType.FALSE;
		public string token;
		public Value  value;

		public FalseNode ( string s ) {
			token = s;
			value = Value.Bool ( bool.Parse ( s ) );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}
	}
	
	public class NilNode : AstNode {
		public override AstNodeType type => AstNodeType.NIL;
		public string token;
		public Value  value;

		public NilNode ( string name ) {
			token = name;
			value = Value.Nil ();
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Literal ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			return value;
		}
	}
	
	public class ArrayNode : AstNode {
		public override AstNodeType type => AstNodeType.ARRAY;
		public IList<AstNode> nodes;
		public Value          value;

		public ArrayNode ( IList<AstNode> nodes ) {
			this.nodes = nodes;
			value = Value.Array ( RArray.CreateArray () );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Array ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			IList< Value > result = value.As< IList< Value > > ();

			foreach ( var node in nodes ) {
				result.Add ( node.Evaluate ( context ) );
			}

			return value;
		}
	}
	
	public class HashNode : AstNode {
		public override AstNodeType type => AstNodeType.HASH;
		public IList<AstNode> keys;
		public IList<AstNode> values;
		public Value          value;

		public HashNode ( IList<AstNode> keys, IList<AstNode> values ) {
			this.keys = keys;
			this.values = values;
			value = Value.Hash ( RHash.CreateHash () );
		}

		public override void Compile ( ByteCode bc ) {
			bc.Emit_Hash ( this );
		}
		
		public override Value Evaluate ( RubyContext context ) {
			
#if DEBUG
			context.VM.CurrentEvalNode = this;
#endif
			
			IDictionary<Value, Value> result = value.As< IDictionary< Value, Value > > ();

			for ( var i = 0; i < keys.Count; ++i ) {
				result.Add ( keys[ i ].Evaluate ( context ), values[ i ].Evaluate ( context ) );
			}
			
			return value;
		}
	}
}
