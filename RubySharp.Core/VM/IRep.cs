namespace RubySharp.Core {
	
	using System.Collections.Generic;
	
	/// <summary>
	/// Compiled ruby scripts.
	/// Program data array struct
	/// </summary>
	public class IRep {

		public const int INIT_ISEQ_SIZE = 1024;
		public const int INIT_POOL_SIZE = 32;
		public const int INIT_SYMS_SIZE = 256;
		public const int INIT_REPS_SIZE = 8;
		
		public int nlocals; /* Number of local variables */
		public int nregs; /* Number of register variables */
		public List< Instr > iseq;
		public List< Value > pool;
		public List< int > syms;
		public List< IRep > reps;
		
		public Dictionary< int, string > symMap;

		
		public IRep () {
			iseq = new List< Instr > ( INIT_ISEQ_SIZE );
			pool = new List< Value > ( INIT_POOL_SIZE );
			syms = new List< int > ( INIT_SYMS_SIZE );
			reps = new List< IRep > ( INIT_REPS_SIZE );
			symMap = new Dictionary< int , string > ( INIT_SYMS_SIZE );
		}

		
		public int AddNewValue ( Value value ) {
			pool.Add ( value );
			return pool.Count - 1;
		}
		
		public int GetSym ( string sym ) {
			int hash = HashCodeUtility.GetPersistentHashCode ( sym );
			if ( syms.Contains ( hash ) ) {
				return syms.IndexOf ( hash );
			}
			return -1;
		}
		
		public int GetOrAddSym ( string sym ) {
			int hash = HashCodeUtility.GetPersistentHashCode ( sym );
			if ( syms.Contains ( hash ) ) {
				return syms.IndexOf ( hash );
			}
			syms.Add ( hash );
			symMap.Add ( hash, sym );
			return pool.Count - 1;
		}
	}
}
