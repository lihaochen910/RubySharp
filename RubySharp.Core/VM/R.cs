using System;


namespace RubySharp.Core {
	
	using System.Collections.Generic;


	public class RBasic {
		public ValueType type;
		public VM        vm;
	}
	
	public class RObject : RBasic {

		public Dictionary< string, Value > iv = new Dictionary< string , Value > ();
		internal RClass @class;
		internal RClass singletonclass;

		public RClass Class {
			get { return @class; }
		}

		public RClass SingletonClass {
			get {
				if ( singletonclass == null ) {
					singletonclass = new RClass { name = string.Format ( "#<Class:{0}>", this.ToString () ), super = this.@class };
					// if ( @class == null ) {
					// 	Console.WriteLine ( $"{this.ToString ()} CreateSingletonClass: @class == null" );
					// }
					singletonclass.SetClass ( this.@class?.@class );
				}

				return singletonclass;
			}
		}
		
		public virtual RObject CreateInstance ( VM vm ) {
			var obj = new RObject ();
			obj.vm = vm;
			obj.@class = @class;
			return obj;
		}

		public static RObject CreateUserdataRObject ( VM vm, RClass @class, object userdata ) {
			var obj = new RObject ();
			obj.vm     = vm;
			obj.type   = ValueType.Data;
			obj.@class = @class;
			obj.SetIV ( VM.FIELD_USERDATA, Value.Ptr ( userdata ) );
			return obj;
		}

		internal void SetClass ( RClass @class ) {
			this.@class = @class;
		}

		public Value GetIV ( string sym ) {
			if ( iv.ContainsKey ( sym ) ) {
				return iv[ sym ];
			}
			return Value.Nil ();
		}

		public void SetIV ( string sym, Value value ) {

			value = value != null ? value : Value.Nil ();
			
			if ( iv.ContainsKey ( sym ) ) {
				iv[ sym ] = value;
			}
			else {
				iv.Add ( sym, value );
			}
		}

		public virtual IFunction GetMethod ( string name ) {
			if ( singletonclass != null ) {
				return singletonclass.GetInstanceMethod ( name );
			}

			if ( @class != null ) {
				return @class.GetInstanceMethod ( name );
			}

			return null;
		}

		public override string ToString () {
			return string.Format ( "#<{0}:0x{1}>", @class.name, GetHashCode ().ToString ( "x" ) );
		}
	}

	public class RClass : RObject {
		public string name;
		public RClass super;
		public RClass parent;
		public IDictionary< string, IFunction > methods = new Dictionary< string, IFunction > ();
		public RubyContext constants;
		
		
		public string FullName {
			get {
				if ( parent == null )
					return name;

				return parent.FullName + "::" + name;
			}
		}
		
		public Value GetCV ( string sym ) {
			return SingletonClass.GetIV ( sym );
		}

		public void SetCV ( string sym, Value value ) {
			SingletonClass.SetIV ( sym, value );
		}
		
		public void SetInstanceMethod ( string name, IFunction method ) {
			methods[ name ] = method;
		}
		
		// public void SetInstanceMethod ( string name, RubyFunction method ) {
		// 	methods[ name ] = new LambdaFunction ( method.Invoke );
		// }
		
		public void SetInstanceMethod ( string name, Func< Value, RubyContext, IList< Value >, Value > lambda ) {
			methods[ name ] = new LambdaFunction ( lambda );
		}
		
		public IFunction GetInstanceMethod ( string name ) {
			
			// Console.WriteLine ( $"{FullName}::GetInstanceMethod() {name}" );
			
			if ( methods.ContainsKey ( name ) ) {
				return methods[ name ];
			}

			if ( super != null ) {
				return super.GetInstanceMethod ( name );
			}

			// base.GetMethod ??
			// return base.GetMethod ( name );
			return null;
		}
		
		public void SetClassMethod ( string name, IFunction method ) {
			SingletonClass.methods[ name ] = method;
		}
		
		public void SetClassMethod ( string name, Func< Value, RubyContext, IList< Value >, Value > method ) {
			SingletonClass.methods[ name ] = new LambdaFunction ( method );
			// Console.WriteLine ( $"SingletonClass:{SingletonClass.GetHashCode()} SetClassMethod {name}" );
		}
		
		public IFunction GetClassMethod ( string name ) {
			// Console.WriteLine ( $"SingletonClass:{SingletonClass.GetHashCode()} GetClassMethod {name}" );
			if ( SingletonClass.methods.ContainsKey ( name ) ) {
				return SingletonClass.methods[ name ];
			}
			
			if ( singletonclass.super != null ) {
				return singletonclass.super.GetInstanceMethod ( name );
			}

			if ( @class != null ) {
				return @class.GetInstanceMethod ( name );
			}

			return null;
		}

		public override string ToString () {
			return FullName;
		}
	}
	
	public class RProc : RBasic {
		
	}
	
	public class RString : RClass {
		
		public RString () {
			SetInstanceMethod ( "+", str_plus_m );
			SetInstanceMethod ( "*", str_times );
		}
		
		public static Value str_plus_m ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.String ) {
				return Value.Str ( ( string )self.p + ( string )y.p );
			}
			if ( y.type == ValueType.Fixnum ) {
				return Value.Str ( ( string )self.p + y.i.ToString () );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Str ( ( string )self.p + y.f.ToString () );
			}

			VM.ThrowException ( $"String.+() non string value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value str_times ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				string elm = ( string )self.p;
				string result = string.Empty;
				for ( var i = 0; i < y.i; ++i ) {
					result += elm;
				}
				return Value.Str ( result );
			}
			if ( y.type == ValueType.Float ) {
				string elm    = ( string )self.p;
				string result = string.Empty;
				for ( var i = 0; i < ( int )y.f; ++i ) {
					result += elm;
				}
				return Value.Str ( result );
			}

			VM.ThrowException ( $"String.*() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
	}
	
	public class RArray : RClass {

		public const int ARRAY_INIT_SIZE = 4;
		
		public RArray () {
			SetInstanceMethod ( "[]", Get );
			SetInstanceMethod ( "[]=", Set );
			
			SetInstanceMethod ( "<<", Push );
			SetInstanceMethod ( "push", Push );
			SetInstanceMethod ( "append", Push );
			SetInstanceMethod ( "pop", Pop );
			SetInstanceMethod ( "clear", Clear );
			
			SetInstanceMethod ( "first", GetFirst );
			SetInstanceMethod ( "last", GetLast );

			SetInstanceMethod ( "each", DoEach );
			SetInstanceMethod ( "size", Size );
			SetInstanceMethod ( "length", Size );
			
			SetInstanceMethod ( VM.TO_S, ToS );
		}
		
		public static IList< Value > CreateArray () {
			return new List< Value > ( RArray.ARRAY_INIT_SIZE );
		}
		
		public static Value Get ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			var index = context.VM.GetArg< Value > ( values, 0 );
			int idx = -1;

			switch ( index.type ) {
				case ValueType.Fixnum:
					idx = index.i;
					break;
				case ValueType.Float:
					idx = (int)index.f;
					break;
				default:
					VM.ThrowException ( $"Array []: {index.type} not impl." );
					break;
			}

			if ( idx >= 0 && idx < array.Count ) {
				return array[ idx ];
			}
			
			return Value.Nil ();
		}
		
		public static Value Set ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			var index = context.VM.GetArg< Value > ( values, 0 );
			var val = context.VM.GetArg< Value > ( values, 1 );
			int idx   = -1;

			switch ( index.type ) {
				case ValueType.Fixnum:
					idx = index.i;
					break;
				case ValueType.Float:
					idx = (int)index.f;
					break;
				default:
					VM.ThrowException ( $"Array []=: {index.type} not impl." );
					break;
			}
			
			if ( idx >= array.Count ) {
				var expand = idx - array.Count + 1;
				for ( var i = 0; i < expand; ++i ) {
					array.Add ( Value.Nil () );
				}
			}
			
			if ( idx >= 0 && idx < array.Count ) {
				return array[ idx ] = val;
			}
			
			return val;
		}
		
		public static Value GetFirst ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			if ( array.Count == 0 ) {
				return Value.Nil ();
			}
			return Get ( self, context, new [] { Value.Fixnum ( 0 ) } );
		}
		
		public static Value GetLast ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			if ( array.Count == 0 ) {
				return Value.Nil ();
			}
			return Get ( self, context, new [] { Value.Fixnum ( array.Count - 1 ) } );
		}
		
		public static Value Push ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			array.Add ( context.VM.GetArg<Value>( values, 0 ) );
			return self;
		}
		
		public static Value Pop ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			if ( array.Count > 0 ) {
				array.RemoveAt ( array.Count - 1 );
			}
			return self;
		}

		public static Value Clear ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			array.Clear ();
			return self;
		}
		
		public static Value DoEach ( Value self, RubyContext context, IList< Value > values ) {
			var array = self.As< IList< Value > > ();
			var block = context.VM.GetArg< BlockFunction > ( values, 0 );

			if ( block == null ) {
				VM.ThrowException ( $"Array.each() arg error: {block}" );
			}

			foreach ( var val in array ) {
				block.Invoke ( self, context, new [] { val } );
			}

			return null;
		}

		public static Value Size ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( self.As< IList< Value > > ().Count );
		}
		
		public static Value ToS ( Value self, RubyContext context, IList< Value > values ) {
			
			var arrayStr = "[";
			var array    = self.As< IList< Value > > ();
			foreach ( var val in array ) {
				if ( array.IndexOf ( val ) != array.Count - 1 ) {
					arrayStr += $"{context.VM.ValueToString ( val )}, ";
				}
				else {
					arrayStr += $"{context.VM.ValueToString ( val )}";
				}
			}
			return Value.Str ( arrayStr + "]" );
		}
	}
	
	public class RHash : RClass {

		public const int HASH_INIT_SIZE = 4;
		
		public RHash () {
			SetInstanceMethod ( "[]", Get );
			SetInstanceMethod ( "[]=", Set );
			
			SetInstanceMethod ( "clear", Clear );
			
			// SetInstanceMethod ( "each", DoEach );
			SetInstanceMethod ( "size", Size );
			SetInstanceMethod ( "length", Size );
		}
		
		static public IDictionary< Value, Value > CreateHash () {
			return new Dictionary< Value , Value >( RHash.HASH_INIT_SIZE );
		}
		
		public static Value Get ( Value self, RubyContext context, IList< Value > values ) {
			var hash = self.As< IDictionary< Value, Value > > ();
			var key = context.VM.GetArg< Value > ( values, 0 );
			
			if ( hash.ContainsKey ( key ) ) {
				return hash[ key ];
			}
			
			return Value.Nil ();
		}
		
		public static Value Set ( Value self, RubyContext context, IList< Value > values ) {
			var hash = self.As< IDictionary< Value, Value > > ();
			var key  = context.VM.GetArg< Value > ( values, 0 );
			var val   = context.VM.GetArg< Value > ( values, 1 );
			
			if ( hash.ContainsKey ( key ) ) {
				return hash[ key ] = val;
			}
			else {
				hash.Add ( key, val );
			}
			
			return val;
		}
		
		public static Value Clear ( Value self, RubyContext context, IList< Value > values ) {
			var hash = self.As< IDictionary< Value, Value > > ();
			hash.Clear ();
			return self;
		}
		
		public static Value DoEach ( Value self, RubyContext context, IList< Value > values ) {
			// var   block = ( Block )values[ 0 ];
			// IList list  = ( IList )obj;
			//
			// foreach ( var value in list )
			// 	block.Apply ( new object[] { value } );

			return null;
		}
		
		public static Value Size ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( self.As< IDictionary< Value, Value > > ().Count );
		}
		
		public static implicit operator Value ( RHash hash ) {
			return new Value { type = ValueType.Hash, p = hash };
		}
	}
}
