namespace RubySharp.Core {

	using System;
	using System.Collections.Generic;
	using RubySharp.Core.Compiler;

	/// <summary>
	/// 执行作用域
	/// </summary>
	public class Scope {

		public const int MAX_STACK_SIZE = 0xffff;
		public const int MAX_INSTR_SIZE = 1 << 16;
		public const int MAX_RLEV_LENGTH = 1024;

		public VM vm;
		public Scope prev;
		public AstNode lv;
		public Value self;

		public IDictionary<string, Value> localVars = new Dictionary<string, Value> ();
		public IDictionary<string, int> localVarsIndex = new Dictionary<string, int> ();

		public int sp;  // 栈指针，指向栈顶位置
		public int pc;  // 当前执行指令的索引
		public int lastpc;
		public int lastlabel;

		public int ensureLevel;
		public string filename;
		public int lineno;


		public Instr Last {
			get
			{
				if ( pc == lastpc ) {
					return new Instr ( OpCode.Nop );
				}

				return vm.bc.Code[ lastpc ];
			}
		}

		public virtual void SetLocalValue ( string name, Value value ) {
			localVars[ name ] = value;
		}

		public virtual bool HasLocalValue ( string name ) {
			return localVars.ContainsKey ( name );
		}

		public bool HasValue ( string name ) {
			if ( HasLocalValue ( name ) )
				return true;

			if ( prev != null )
				return prev.HasValue ( name );

			return false;
		}

		public virtual Value GetLocalValue ( string name ) {
			return localVars[ name ];
		}

		public Value GetValue ( string name ) {
			if ( localVars.ContainsKey ( name ) )
				return localVars[ name ];

			if ( prev != null )
				return prev.GetValue ( name );

			return null;
		}


		/// <summary>
		/// 向作用域添加一个局部变量
		/// </summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns>Value堆栈索引</returns>
		public int AddLocalValue ( string name, Value value = null ) {
			if ( HasLocalValue ( name ) ) {
				throw new Exception ( $"{name} already defined in Current Scope." );
			}

			if ( value == null ) {
				value = Value.Nil ();
			}

			SetLocalValue ( name, value );
			localVarsIndex.Add ( name, vm.PushValue ( value ).ValueStackTopIndex );

			return localVarsIndex[ name ];
		}


		/// <summary>
		/// 获取局部变量在VM堆栈中的索引
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public int GetLocalValueIdx ( string name ) {
			if ( HasLocalValue ( name ) ) {
				return localVarsIndex[ name ];
			}
			return -1;
		}


		/// <summary>
		/// 获取变量在VM堆栈中的索引(将会一直查找到顶部)
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public int GetValueIdx ( string name ) {

			if ( HasValue ( name ) ) {

				Scope current = this;
				while ( current != null ) {

					if ( current.HasLocalValue ( name ) )
						return current.localVarsIndex[ name ];

					current = current.prev;
				}
			}
			return -1;
		}
	}
}
