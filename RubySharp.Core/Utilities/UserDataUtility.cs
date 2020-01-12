namespace RubySharp.Core {
	
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using Fasterflect;
	
	/// <summary>
	/// 导入C#类到RubySharpVM中的低性能实现
	/// </summary>
	public class UserDataUtility {
		
		private static Dictionary<string, string> operator_methods = new Dictionary<string, string> {
			// { "op_LogicalNot", "!" },
			{ "op_Addition", "+" }, { "op_Subtraction", "-" }, { "op_Multiply", "*" },
			{ "op_Division", "/" }, { "op_BitwiseAnd", "&" }, { "op_BitwiseOr", "|" },
			{ "op_ExclusiveOr", "^" }, { "op_OnesComplement", "~" }, { "op_Equality", "==" },
			{ "op_Inequality", "!=" }, { "op_LessThan", "<" }, { "op_GreaterThan", ">" },
			{ "op_LessThanOrEqual", "<=" }, { "op_GreaterThanOrEqual", ">=" }, { "op_LeftShift", "<<" },
			{ "op_RightShift", ">>" }, { "op_Modulus", "%" }
		};

		public static void RegAssembly ( VM vm, Assembly assembly ) {
			var types = assembly.GetTypes ();
			foreach ( var type in types ) {
				RegCustomClass ( vm, type );
			}
		}

		public static void RegNamespace ( VM vm, Assembly assembly, string @namespace ) {
			Type[] types = assembly.GetTypes ()
			                       .Where ( t => String.Equals ( t.Namespace, @namespace, StringComparison.Ordinal ) )
			                       .ToArray ();
			foreach ( var type in types ) {
				RegCustomClass ( vm, type );
			}
		}
		
		public static RClass RegCustomClass<T> ( VM vm ) {
			Type type = typeof ( T );
			return RegCustomClass ( vm, type );
		}

		public static RClass RegCustomClass ( VM vm, Type type ) {
			
			// Console.WriteLine ( $"Namespace: {type.Namespace}" );
			// Console.WriteLine ( $"FullName: {type.FullName}" );

			RClass @class = null;
			string[] namespacePath = type.FullName.Split ( '.' );
			foreach ( var className in namespacePath ) {
				if ( className.Equals ( namespacePath[ 0 ] ) ) {
					@class = vm.DefClass ( className, vm.object_class );
				}
				else {
					@class = vm.DefClass ( className, vm.object_class, @class );
				}
			}
			
			vm.WriteCustomClassFlag ( @class, type );
			vm.WriteCustomClassRClass ( @class, @class );
			
			ConstructorInfo publicCtor = type.Constructors ( Flags.InstancePublic ).OrderBy ( c => c.GetParameters ().Length ).FirstOrDefault ();
			IList<FieldInfo> publicStaticFields = type.Fields ( Flags.StaticPublicDeclaredOnly );
			IList<FieldInfo> publicFields = type.Fields ( Flags.InstancePublic );
			IList<PropertyInfo> publicPropertys = type.Properties ( Flags.InstancePublic );
			IList<MethodInfo> publicMethods = type.Methods ( Flags.InstancePublic );
			IList<MethodInfo> publicStaticMethods = type.Methods ( Flags.StaticPublic );
			
			if ( publicCtor != null ) {
				@class.SetInstanceMethod ( VM.INITIALIZE, GenCtor ( publicCtor ) );
			}
			
			foreach ( var field in publicStaticFields ) {
				@class.SetInstanceMethod ( field.Name, GenGetField ( field ) );
			}

			foreach ( var field in publicFields ) {
				@class.SetInstanceMethod ( field.Name, GenGetField ( field ) );
				@class.SetInstanceMethod ( $"{field.Name}=", GenSetField ( field ) );
			}
			
			foreach ( var property in publicPropertys ) {
				if ( property.CanRead ) {
					@class.SetInstanceMethod ( property.Name, GenGetProperty ( property ) );
				}

				if ( property.CanWrite ) {
					@class.SetInstanceMethod ( $"{property.Name}=", GenSetProperty ( property ) );
				}
			}
			
			foreach ( var method in publicMethods ) {
				@class.SetInstanceMethod ( method.Name, GenMethod ( method ) );
			}
			
			//foreach ( var method in publicStaticMethods ) {
			//	@class.SetClassMethod ( method.Name, GenMethod ( method, true ) );
			//}
			
			foreach ( var kv in operator_methods ) {
				Gen_BaseOp_IfExist ( type, @class, kv.Key );
			}
			
			@class.SetInstanceMethod ( VM.TO_S, GenToString ( type.Method ( "ToString", Type.EmptyTypes ) ) );
			@class.SetInstanceMethod ( VM.TO_STR, GenToString ( type.Method ( "ToString", Type.EmptyTypes ) ) );
			
			return @class;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenCtor ( ConstructorInfo ctorInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				//Console.WriteLine ( $"C# ctor {ctorInfo} called!" );
				var paramsInfo = ctorInfo.Parameters ();
				object[] ctorParams = new object[ paramsInfo.Count ];
				if ( values != null ) {
					for ( var i = 0; i < values.Count; ++i ) {
						ctorParams[ i ] = context.VM.ValueToObject ( values[ i ] );
					}
				}
				Value ret = Value.Ptr ( ctorInfo.Invoke ( ctorParams ) );
				//Console.WriteLine ( $"写入Userdata:{ret}到RObject:{self}" );
				// 写入C#实例到RObject中
				( (RObject)self.p ).SetIV ( VM.FIELD_USERDATA, ret );
				return ret;
			};
			return func;
		}

		public static Func< Value, RubyContext, IList< Value >, Value > GenGetField ( FieldInfo fieldInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				if ( fieldInfo.FieldType == typeof ( int ) ) {
					return Value.Fixnum ( ( int )fieldInfo.GetValue ( userdata ) );
				}
				if ( fieldInfo.FieldType == typeof ( float ) ) {
					return Value.Float ( ( float )fieldInfo.GetValue ( userdata ) );
				}
				if ( fieldInfo.FieldType == typeof ( bool ) ) {
					return Value.Bool ( ( bool )fieldInfo.GetValue ( userdata ) );
				}
				if ( fieldInfo.FieldType == typeof ( string ) ) {
					return Value.Str ( ( string )fieldInfo.GetValue ( userdata ) );
				}
				return Value.Data ( RObject.CreateUserdataRObject ( context.VM, ( ( RObject )self.p ).Class, fieldInfo.GetValue ( userdata ) ) );
				// return Value.Ptr ( fieldInfo.GetValue ( userdata ) );
			};
			return func;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenSetField ( FieldInfo fieldInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				if ( fieldInfo.FieldType == typeof ( int ) ) {
					fieldInfo.SetValue ( userdata, context.VM.GetArg< int > ( values, 0 ) );
				}
				else if ( fieldInfo.FieldType == typeof ( float ) ) {
					fieldInfo.SetValue ( userdata, context.VM.GetArg< float > ( values, 0 ) );
				}
				else if ( fieldInfo.FieldType == typeof ( bool ) ) {
					fieldInfo.SetValue ( userdata, context.VM.GetArg< bool > ( values, 0 ) );
				}
				else if ( fieldInfo.FieldType == typeof ( string ) ) {
					fieldInfo.SetValue ( userdata, context.VM.GetArg< string > ( values, 0 ) );
				}
				else {
					fieldInfo.SetValue ( userdata, context.VM.GetArg< object > ( values, 0 ) );
				}
				return Value.Nil ();
			};
			return func;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenGetProperty ( PropertyInfo propertyInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				if ( propertyInfo.PropertyType == typeof ( int ) ) {
					return Value.Fixnum ( ( int )propertyInfo.GetValue ( userdata, null ) );
				}
				if ( propertyInfo.PropertyType == typeof ( float ) ) {
					return Value.Float ( ( float )propertyInfo.GetValue ( userdata, null ) );
				}
				if ( propertyInfo.PropertyType == typeof ( bool ) ) {
					return Value.Bool ( ( bool )propertyInfo.GetValue ( userdata, null ) );
				}
				if ( propertyInfo.PropertyType == typeof ( string ) ) {
					return Value.Str ( ( string )propertyInfo.GetValue ( userdata, null ) );
				}
				return Value.Data ( RObject.CreateUserdataRObject ( context.VM, ( ( RObject )self.p ).Class, propertyInfo.GetValue ( userdata, null ) ) );
				// return Value.Ptr ( propertyInfo.GetValue ( userdata ) );
			};
			return func;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenSetProperty ( PropertyInfo propertyInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				if ( propertyInfo.PropertyType == typeof ( int ) ) {
					propertyInfo.SetValue ( userdata, context.VM.GetArg< int > ( values, 0 ), null );
				}
				else if ( propertyInfo.PropertyType == typeof ( float ) ) {
					propertyInfo.SetValue ( userdata, context.VM.GetArg< float > ( values, 0 ), null );
				}
				else if ( propertyInfo.PropertyType == typeof ( bool ) ) {
					propertyInfo.SetValue ( userdata, context.VM.GetArg< bool > ( values, 0 ), null );
				}
				else if ( propertyInfo.PropertyType == typeof ( string ) ) {
					propertyInfo.SetValue ( userdata, context.VM.GetArg< string > ( values, 0 ), null );
				}
				else {
					propertyInfo.SetValue ( userdata, context.VM.GetArg< object > ( values, 0 ), null );
				}
				return Value.Nil ();
			};
			return func;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenMethod ( MethodInfo methodInfo, bool staticMethod = false ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				var parametersInfo = methodInfo.Parameters ();
				object[] parameters = new object[ parametersInfo.Count ];
				for ( var i = 0; i < parametersInfo.Count; ++i ) {
					parameters[ i ] = context.VM.ValueToObject ( context.VM.GetArg< Value > ( values, i ) );
				}
				object ret = methodInfo.Invoke ( staticMethod ? null : userdata, parameters );
				if ( ret == null ) {
					return Value.Nil ();
				}
				if ( ret.GetType () == typeof ( int ) ) {
					return Value.Fixnum ( ( int )ret );
				}
				if ( ret.GetType () == typeof ( float ) ) {
					return Value.Float ( ( float )ret );
				}
				if ( ret.GetType () == typeof ( bool ) ) {
					return Value.Bool ( ( bool )ret );
				}
				if ( ret.GetType () == typeof ( string ) ) {
					return Value.Str ( ( string )ret );
				}
				return Value.Data ( RObject.CreateUserdataRObject ( context.VM, ( ( RObject )self.p ).Class, ret ) );
			};
			return func;
		}
		
		public static Func< Value, RubyContext, IList< Value >, Value > GenToString ( MethodInfo methodInfo ) {
			Func< Value, RubyContext, IList< Value >, Value > func = ( self, context, values ) => {
				//Console.WriteLine ( $"从RObject:{self}读取Userdata:{ ( (RObject)self.p ).GetIV ( VM.FIELD_USERDATA )}" );
				object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
				return Value.Str ( ( string )methodInfo.Invoke ( userdata, null ) );
			};
			return func;
		}

		public static Func< Value, RubyContext, IList< Value >, Value > Gen_BaseOp_IfExist ( Type type, RClass @class, string op ) {
			
			var methodInfo = type.GetMethods ( BindingFlags.Static | BindingFlags.Public )
			                 .Where ( m => {
				                 if ( !m.Name.Equals ( op ) ) {
					                 return false;
				                 }
				                 var parameters = m.GetParameters ();
				                 if ( parameters.Length != 2 ) {
					                 return false;
				                 }
				                 if ( parameters[ 0 ].ParameterType != type ) {
					                 return false;
				                 }
				                 return true;
			                 } ).FirstOrDefault ();
			
			if ( methodInfo != null ) {
				
				Func< Value, RubyContext, IList< Value >, Value > func = ( self , context , values ) => {
					object userdata = ( ( RObject )self.p ).GetIV ( VM.FIELD_USERDATA ).p;
					object[] parameters = new [] {
						userdata,
						context.VM.ValueToObject ( context.VM.GetArg< Value > ( values, 0 ) )
					};

					object ret = methodInfo.Invoke ( null, parameters );
					if ( ret == null ) {
						return Value.Nil ();
					}
					if ( ret.GetType () == typeof ( int ) ) {
						return Value.Fixnum ( ( int )ret );
					}
					if ( ret.GetType () == typeof ( float ) ) {
						return Value.Float ( ( float )ret );
					}
					if ( ret.GetType () == typeof ( bool ) ) {
						return Value.Bool ( ( bool )ret );
					}
					if ( ret.GetType () == typeof ( string ) ) {
						return Value.Str ( ( string )ret );
					}
					return Value.Data ( RObject.CreateUserdataRObject ( context.VM, context.VM.FindCustomClass ( ret ), ret ) );
				};
				
				@class.SetInstanceMethod ( operator_methods[ op ], func );
				
				return func;
			}

			return null;
		}
	}
}
