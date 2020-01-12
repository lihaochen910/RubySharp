namespace RubySharp.Core {
	
	using System.Collections;
	using System.Collections.Generic;
	
	public class RangeClass : RClass {

		internal RangeClass () {
			SetInstanceMethod ( VM.EACH, range_each );
			SetInstanceMethod ( VM.TO_A, range_to_a );
		}
		
		public static Value range_each ( Value self, RubyContext context, IList< Value > values ) {
			
			var range = self.As< Range > ();
			var block = context.VM.GetArg< BlockFunction > ( values, 0 );

			if ( block == null ) {
				VM.ThrowException ( $"Range.each() arg error: {block}" );
			}

			foreach ( var val in range ) {
				block.Invoke ( self, context, new [] { Value.Fixnum ( val ) } );
			}

			return null;
		}
		
		public static Value range_to_a ( Value self, RubyContext context, IList< Value > values ) {
			
			var range = self.As< Range > ();
			var array = RArray.CreateArray ();
			
			for ( var i = range.@from; i <= range.to; ++i ) {
				array.Add ( context.VM.rb_fixnum_value ( i ) );
			}

			return Value.Array ( array );
		}
	}

	public class Range : IEnumerable< int > {
		
		public int from;
		public int to;

		public Range ( int from, int to ) {
			this.from = from;
			this.to   = to;
		}

		public IEnumerator< int > GetEnumerator () {
			return new RangeEnumerator ( from, to );
		}

		IEnumerator IEnumerable.GetEnumerator () {
			return new RangeEnumerator ( from, to );
		}

		public override string ToString () {
			return string.Format ( "{0}..{1}", from, to );
		}

		private class RangeEnumerator : IEnumerator< int > {
			private int from;
			private int to;
			private int current;

			public RangeEnumerator ( int from, int to ) {
				this.from    = from;
				this.to      = to;
				this.current = from - 1;
			}

			int IEnumerator< int >.Current {
				get { return current; }
			}

			public object Current {
				get { return current; }
			}

			public bool MoveNext () {
				current++;

				return current >= from && current <= to;
			}

			public void Reset () {
				current = from - 1;
			}

			public void Dispose () {}
		}
	}
}
