using System;


namespace RubySharp.Core {
	
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using RubySharp.Core.Language;
	
//	[StructLayout ( LayoutKind.Explicit, Size = 8 )]
	public class Value {
		
		/*
		 * 空值: type == False && i == 0
		 */
		
		/*[FieldOffset ( 0 )]*/ public ValueType type;
		
		/*[FieldOffset ( 1 )]*/ public int    i = Int32.MinValue; // 整型字面量
		/*[FieldOffset ( 1 )]*/ public float  f = Single.MinValue; // 浮点型字面量
		/*[FieldOffset ( 1 )]*/ public object p; // 引用类型值



		public static implicit operator bool ( Value val ) {
			
			if ( val == null ) {
				return false;
			}

			if ( val.type == ValueType.False ) {
				return false;
			}
			
			if ( val.type == ValueType.False && val.i == 0 ) {
				return false;
			}
			
			return true;
		}
		
		
		// public static implicit operator object ( Value val ) {
		// 	return val.p;
		// }


		public override string ToString () {
			switch ( type ) {
				case ValueType.False:
					if ( i == 0 ) {
						return string.Empty;
					}
					return "False";
				case ValueType.True: return "True";
				case ValueType.Fixnum: return i.ToString ();
				case ValueType.Float: return f.ToString ();
				case ValueType.Symbol:
				case ValueType.CPtr:
				case ValueType.Object:
				case ValueType.Class:
				case ValueType.Module:
				case ValueType.String:
				case ValueType.Range:
					return p.ToString ();
				case ValueType.Array:
					var arrayStr = "[";
					var array = As< IList< Value > > ();
					foreach ( var val in As< IList< Value > >() ) {
						if ( array.IndexOf ( val ) != array.Count - 1 ) {
							arrayStr += $"{val}, ";
						}
						else {
							arrayStr += $"{val}";
						}
					}
					return arrayStr + "]";
				case ValueType.Hash:
					var hashStr = "{";
					foreach ( var val in As< IDictionary< Value, Value > >() ) {
						hashStr += $" {val.Key} => {val.Value}, ";
					}
					return hashStr + "}";
			}
			
			return $"Value type:{type} i:{i} f:{f} p:{p}";
		}


		public T As< T > (){
			if ( typeof ( T ) == typeof ( int ) ) {
				return ( T )( object )i;
			}
			if ( typeof ( T ) == typeof ( float ) ) {
				return ( T )( object )f;
			}
			return ( T )p;
		}
		
		
		public static Value Fixnum ( int num ) {
			return new Value { type = ValueType.Fixnum, i = num };
		}
		
		
		public static Value Float ( float num ) {
			return new Value { type = ValueType.Float, f = num };
		}
		
		
		public static Value Float ( double num ) {
			return new Value { type = ValueType.Float, f = ( float )num };
		}
		
		
		public static Value Str ( string str ) {
			return new Value { type = ValueType.String, p = str };
		}


		public static Value Bool ( bool b ) {
			return new Value { type = b ? ValueType.True : ValueType.False, i = Int32.MinValue };
		}


		public static Value Nil () {
			return new Value { type = ValueType.False, i = 0 };
		}
		
		
		public static Value Symbol ( Symbol sym ) {
			return new Value { type = ValueType.Symbol, p = sym };
		}


		public static Value Array ( IList< Value > array ) {
			return new Value { type = ValueType.Array, p = array };
		}


		public static Value Hash ( IDictionary< Value, Value > hash ) {
			return new Value { type = ValueType.Hash, p = hash };
		}
		
		
		public static Value Proc ( IFunction function ) {
			return new Value { type = ValueType.Proc, p = function };
		}
		
		
		public static Value Range ( Range range ) {
			return new Value { type = ValueType.Range, p = range };
		}


		public static Value Object ( object obj ) {
			return new Value { type = ValueType.Object, p = obj };
		}
		
		
		public static Value Ptr ( object obj ) {
			return new Value { type = ValueType.CPtr, p = obj };
		}
		
		
		public static Value Data ( RObject obj ) {
			return new Value { type = ValueType.Data, p = obj };
		}
		
		
		public static Value Class ( object cls ) {
			return new Value { type = ValueType.Class, p = cls };
		}
	}

	public enum ValueType : byte {
		False,
		Free,
		True,
		Fixnum,
		Symbol,
		Undef,
		Float,
		CPtr,
		Object,
		Class,
		Module,
		IClass,
		SClass,
		Proc,
		Array,
		Hash,
		String,
		Range,
		Exception,
		File,
		Env,
		Data,
		Fiber,
		IStruct,
		Break,
		MaxDefine
	}
}
