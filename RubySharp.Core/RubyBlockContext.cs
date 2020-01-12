namespace RubySharp.Core {
	
	using System;
	using System.Collections.Generic;


	public class RubyBlockContext : RubyContext {
		
		public RubyBlockContext ( RubyContext parent ) : base ( parent ) {}

		public override bool HasLocalValue ( string name ) {
			if ( base.HasLocalValue ( name ) )
				return true;

			return Parent.HasLocalValue ( name );
		}

		public override Value GetLocalValue ( string name ) {
			if ( base.HasLocalValue ( name ) )
				return base.GetLocalValue ( name );

			return Parent.GetLocalValue ( name );
		}

		public override void SetLocalValue ( string name, Value value ) {
			if ( Parent.HasLocalValue ( name ) )
				Parent.SetLocalValue ( name, value );
			else
				base.SetLocalValue ( name, value );
		}
	}
}
