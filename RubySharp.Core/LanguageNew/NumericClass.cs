namespace RubySharp.Core {
	
	using System;
	using System.Collections.Generic;
	
	public class NumericClass : RClass {

		internal NumericClass () {
			SetInstanceMethod ( "**", num_pow );
			SetInstanceMethod ( "/", num_div );
			SetInstanceMethod ( "<=>", integral_cmp );
			SetInstanceMethod ( "<", integral_lt );
			SetInstanceMethod ( "<=", integral_le );
			SetInstanceMethod ( ">", integral_gt );
			SetInstanceMethod ( ">=", integral_ge );
		}
		
		public static Value num_pow ( Value self, RubyContext context, IList< Value > values ) {
			float y = context.VM.GetArg< float > ( values, 0 );
			return Value.Float ( Math.Pow ( context.VM.rb_float ( self ), y ) );
		}
		
		public static Value num_div ( Value self, RubyContext context, IList< Value > values ) {
			float y = context.VM.GetArg< float > ( values, 0 );
			return Value.Float ( context.VM.rb_float ( self ) / y );
		}
		
		public static Value integral_cmp ( Value self, RubyContext context, IList< Value > values ) {
			float x = context.VM.rb_float ( self );
			Value val = context.VM.GetArg< Value > ( values, 0 );
			
			if ( val.type != ValueType.Fixnum &&
			     val.type != ValueType.Float ) {
				return Value.Nil ();
			}
			
			float y = context.VM.rb_float ( val );
			
			if ( x > y ) {
				return Value.Fixnum ( 1 );
			}
			if ( x < y ) {
				return Value.Fixnum ( -1 );
			}
			
			return Value.Fixnum ( 0 );
		}
		
		public static Value integral_lt ( Value self, RubyContext context, IList< Value > values ) {
			float x   = context.VM.rb_float ( self );
			Value val = context.VM.GetArg< Value > ( values, 0 );
			
			if ( val.type != ValueType.Fixnum &&
			     val.type != ValueType.Float ) {
				return Value.Nil ();
			}
			
			float y = context.VM.rb_float ( val );
			
			if ( x < y ) {
				return Value.Bool ( true );
			}
			
			return Value.Bool ( false );
		}
		
		public static Value integral_le ( Value self, RubyContext context, IList< Value > values ) {
			float x   = context.VM.rb_float ( self );
			Value val = context.VM.GetArg< Value > ( values, 0 );
			
			if ( val.type != ValueType.Fixnum &&
			     val.type != ValueType.Float ) {
				return Value.Nil ();
			}
			
			float y = context.VM.rb_float ( val );
			
			if ( x <= y ) {
				return Value.Bool ( true );
			}
			
			return Value.Bool ( false );
		}
		
		public static Value integral_gt ( Value self, RubyContext context, IList< Value > values ) {
			float x   = context.VM.rb_float ( self );
			Value val = context.VM.GetArg< Value > ( values, 0 );
			
			if ( val.type != ValueType.Fixnum &&
			     val.type != ValueType.Float ) {
				return Value.Nil ();
			}
			
			float y = context.VM.rb_float ( val );
			
			if ( x > y ) {
				return Value.Bool ( true );
			}
			
			return Value.Bool ( false );
		}
		
		public static Value integral_ge ( Value self, RubyContext context, IList< Value > values ) {
			float x   = context.VM.rb_float ( self );
			Value val = context.VM.GetArg< Value > ( values, 0 );
			
			if ( val.type != ValueType.Fixnum &&
			     val.type != ValueType.Float ) {
				return Value.Nil ();
			}
			
			float y = context.VM.rb_float ( val );
			
			if ( x >= y ) {
				return Value.Bool ( true );
			}
			
			return Value.Bool ( false );
		}
	}
	
	public class IntegerClass : RClass {

		internal IntegerClass () {
			SetInstanceMethod ( VM.TO_I, int_to_i );
			SetInstanceMethod ( VM.TO_INT, int_to_i );
			SetInstanceMethod ( "ceil", int_to_i );
			SetInstanceMethod ( "floor", int_to_i );
			SetInstanceMethod ( "round", int_to_i );
			SetInstanceMethod ( "truncate", int_to_i );
		}
		
		public static Value int_to_i ( Value self, RubyContext context, IList< Value > values ) {
			return self;
		}
	}
	
	public class FixnumClass : RClass {

		internal FixnumClass () {
			SetInstanceMethod ( "+", fix_plus );
			SetInstanceMethod ( "-", fix_minus );
			SetInstanceMethod ( "*", fix_mul );
			SetInstanceMethod ( "%", fix_mod );
			SetInstanceMethod ( "==", fix_equal );
			SetInstanceMethod ( "&", fix_and );
			SetInstanceMethod ( "|", fix_or );
			SetInstanceMethod ( "^", fix_xor );
			SetInstanceMethod ( "<<", fix_lshift );
			SetInstanceMethod ( ">>", fix_rshift );
			SetInstanceMethod ( "eql?", fix_equal );
			SetInstanceMethod ( VM.TO_F, fix_to_f );
			SetInstanceMethod ( VM.TO_S, fix_to_s );
			SetInstanceMethod ( "inspect", fix_to_s );
		}
		
		public static Value fix_plus ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i + y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i + ( int )y.f );
			}

			VM.ThrowException ( $"Fixnum.+() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_minus ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i - y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i - ( int )y.f );
			}

			VM.ThrowException ( $"Fixnum.-() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_mul ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i * y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Float ( self.i * y.f );
			}

			VM.ThrowException ( $"Fixnum.*() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_mod ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i % y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i % ( int )y.f );
			}

			VM.ThrowException ( $"Fixnum.%() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_equal ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Bool ( self.i == y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Bool ( ( float )self.i == y.f );
			}
			return Value.Bool ( false );
		}
		
		public static Value fix_and ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i & y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i & ( int )y.f );
			}
			VM.ThrowException ( $"Fixnum.&() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_or ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i | y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i | ( int )y.f );
			}
			VM.ThrowException ( $"Fixnum.|() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_xor ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i ^ y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i ^ ( int )y.f );
			}
			VM.ThrowException ( $"Fixnum.^() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_lshift ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i << y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i << ( int )y.f );
			}
			VM.ThrowException ( $"Fixnum.<<() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_rshift ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( self.i >> y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( self.i >> ( int )y.f );
			}
			VM.ThrowException ( $"Fixnum.>>() non fixnum value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value fix_to_f ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Float ( ( float )self.i );
		}
		
		public static Value fix_to_s ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Str ( self.i.ToString () );
		}
	}
	
	public class FloatClass : RClass {

		internal FloatClass () {
			SetInstanceMethod ( "+", flo_plus );
			SetInstanceMethod ( "-", flo_minus );
			SetInstanceMethod ( "*", flo_mul );
			SetInstanceMethod ( "%", flo_mod );
			SetInstanceMethod ( "==", flo_eq );
			SetInstanceMethod ( "&", flo_and );
			SetInstanceMethod ( "|", flo_or );
			SetInstanceMethod ( "^", flo_xor );
			SetInstanceMethod ( "<<", flo_lshift );
			SetInstanceMethod ( ">>", flo_rshift );
			SetInstanceMethod ( "ceil", flo_ceil );
			SetInstanceMethod ( "finite?", flo_finite_p );
			SetInstanceMethod ( "floor", flo_floor );
			SetInstanceMethod ( "infinite?", flo_infinite_p );
			SetInstanceMethod ( "round", flo_round );
			SetInstanceMethod ( "eql?", flo_eq );
			SetInstanceMethod ( VM.TO_F, flo_to_f );
			SetInstanceMethod ( VM.TO_I, flo_truncate );
			SetInstanceMethod ( VM.TO_INT, flo_truncate );
			SetInstanceMethod ( "truncate", flo_truncate );
			SetInstanceMethod ( VM.TO_S, flo_to_s );
			SetInstanceMethod ( "inspect", flo_to_s );
			SetInstanceMethod ( "nan?", flo_nan_p );
			
			SetCV ( "INFINITY", Value.Float ( 1.0f / 0.0f ) );
			SetCV ( "NAN", Value.Float ( 0.0f / 0.0f ) );
		}
		
		public static Value flo_plus ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Float ( self.f + ( float )y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Float ( self.f + y.f );
			}

			VM.ThrowException ( $"Float.+() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_minus ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Float ( self.f - ( float )y.f );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Float ( self.f - y.f );
			}

			VM.ThrowException ( $"Float.-() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_mul ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Float ( self.f * ( float )y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Float ( self.f * y.f );
			}

			VM.ThrowException ( $"Float.*() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_mod ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Float ( self.f % y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Float ( self.f % y.f );
			}

			VM.ThrowException ( $"Float.%() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_eq ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Bool ( self.f == ( float )y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Bool ( self.f == y.f );
			}
			return Value.Bool ( false );
		}
		
		public static Value flo_and ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( ( int )self.f & y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( ( int )self.f & ( int )y.f );
			}
			VM.ThrowException ( $"Float.&() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_or ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( ( int )self.f | y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( ( int )self.f | ( int )y.f );
			}
			VM.ThrowException ( $"Float.|() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_xor ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( ( int )self.f ^ y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( ( int )self.f ^ ( int )y.f );
			}
			VM.ThrowException ( $"Float.^() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_lshift ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( ( int )self.f << y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( ( int )self.f << ( int )y.f );
			}
			VM.ThrowException ( $"Float.<<() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_rshift ( Value self, RubyContext context, IList< Value > values ) {
			var y = context.VM.GetArg< Value > ( values, 0 );
			if ( y.type == ValueType.Fixnum ) {
				return Value.Fixnum ( ( int )self.f >> y.i );
			}
			if ( y.type == ValueType.Float ) {
				return Value.Fixnum ( ( int )self.f >> ( int )y.f );
			}
			VM.ThrowException ( $"Float.>>() non float value {y} (TypeError)" );
			return Value.Nil ();
		}
		
		public static Value flo_ceil ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( ( int )Math.Ceiling ( self.f ) );
		}
		
		public static Value flo_finite_p ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Bool ( !float.IsInfinity ( self.f ) );
		}
		
		public static Value flo_floor ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( ( int )Math.Floor ( self.f ) );
		}
		
		public static Value flo_infinite_p ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Bool ( float.IsInfinity ( self.f ) );
		}
		
		public static Value flo_round ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( ( int )Math.Round ( self.f ) );
		}
		
		public static Value flo_to_f ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Float ( self.f );
		}
		
		public static Value flo_truncate ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Fixnum ( ( int )self.f );
		}
		
		public static Value flo_to_s ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Str ( self.f.ToString () );
		}
		
		public static Value flo_nan_p ( Value self, RubyContext context, IList< Value > values ) {
			return Value.Bool ( float.IsNaN ( self.f ) );
		}
	}
}
