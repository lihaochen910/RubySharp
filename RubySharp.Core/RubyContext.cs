namespace RubySharp.Core {

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using RubySharp.Core.Language;


	public class RubyContext {

		private VM vm;
		private RubyContext parent;
		private IDictionary<string, Value> values = new Dictionary<string, Value> ();
		private RObject self;
		private RClass module;

		//internal string superFunctionName;
		//internal string superFunctionName;

		internal RubyContext ()
			: this ( null ) { }

		internal RubyContext ( RubyContext parent ) {
			this.parent = parent;
		}

		internal RubyContext ( RClass module, RubyContext parent ) {
			this.module = module;
			this.parent = parent;
			self = module;
		}

		internal RubyContext ( RObject self, RubyContext parent ) {
			this.self = self;
			this.parent = parent;
		}

		public RObject Self {
			get { return self; }
			internal set { self = value; }
		}

		public RClass Module {
			get { return module; }
		}

		public RubyContext Parent {
			get { return parent; }
		}

		public RubyContext RootContext {
			get
			{
				if ( parent == null )
					return this;

				return parent.RootContext;
			}
		}

		public VM VM {
			get { return vm; }
			internal set { vm = value; }
		}

		public virtual void SetLocalValue ( string name, Value value ) {
			values[ name ] = value;
		}

		public virtual bool HasLocalValue ( string name ) {
			return values.ContainsKey ( name );
		}

		public bool HasValue ( string name ) {
			if ( HasLocalValue ( name ) )
				return true;

			if ( parent != null )
				return parent.HasValue ( name );

			return false;
		}

		public virtual Value GetLocalValue ( string name ) {
			return values[ name ];
		}

		public Value GetValue ( string name ) {
			if ( values.ContainsKey ( name ) )
				return values[ name ];

			if ( parent != null )
				return parent.GetValue ( name );

			return null;
		}

		public IList<string> GetLocalNames () {
			return values.Keys.ToList ();
		}
	}
}
