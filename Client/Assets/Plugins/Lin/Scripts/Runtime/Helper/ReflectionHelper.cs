/*
┌────────────────────────────┐
│　Description: 反射辅助类
│　Remark: 提供常用的反射操作方法
└────────────────────────────┘
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ZLinq;

namespace Lin.Runtime.Helper
{
    /// <summary>
    /// 反射辅助类，提供常用的反射操作方法
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// 调用静态方法（无返回值）
        /// </summary>
        /// <param name="typeName">类型全名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="args">方法参数</param>
        /// <exception cref="ArgumentNullException">当typeName或methodName为null时抛出</exception>
        /// <exception cref="TypeLoadException">当无法找到指定类型时抛出</exception>
        /// <exception cref="MissingMethodException">当无法找到指定方法时抛出</exception>
        public static void InvokeStaticMethod(string typeName, string methodName, params object[] args)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            Type type = Type.GetType(typeName);
            if (type == null)
                throw new TypeLoadException($"无法找到类型: {typeName}");

            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException($"无法找到静态方法: {typeName}.{methodName}");

            method.Invoke(null, args);
        }

        /// <summary>
        /// 调用静态方法并返回结果
        /// </summary>
        /// <param name="typeName">类型全名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="args">方法参数</param>
        /// <returns>方法执行结果</returns>
        /// <exception cref="ArgumentNullException">当typeName或methodName为null时抛出</exception>
        /// <exception cref="TypeLoadException">当无法找到指定类型时抛出</exception>
        /// <exception cref="MissingMethodException">当无法找到指定方法时抛出</exception>
        public static object CallStaticMethod(string typeName, string methodName, params object[] args)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));

            Type type = Type.GetType(typeName);
            if (type == null)
                throw new TypeLoadException($"无法找到类型: {typeName}");

            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException($"无法找到静态方法: {typeName}.{methodName}");

            return method.Invoke(null, args);
        }

        /// <summary>
        /// 调用静态方法并返回指定类型的结果
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="typeName">类型全名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="args">方法参数</param>
        /// <returns>转换为指定类型的方法执行结果</returns>
        /// <exception cref="ArgumentNullException">当typeName或methodName为null时抛出</exception>
        /// <exception cref="TypeLoadException">当无法找到指定类型时抛出</exception>
        /// <exception cref="MissingMethodException">当无法找到指定方法时抛出</exception>
        /// <exception cref="InvalidCastException">当返回值无法转换为指定类型时抛出</exception>
        public static T CallStaticMethod<T>(string typeName, string methodName, params object[] args)
        {
            var result = CallStaticMethod(typeName, methodName, args);
            if (result == null && !typeof(T).IsValueType)
                return default(T);
            
            try
            {
                return (T)result;
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException($"无法将返回值从 {result?.GetType()?.Name ?? "null"} 转换为 {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 获取静态字段的值
        /// </summary>
        /// <typeparam name="T">字段类型</typeparam>
        /// <param name="typeName">类型全名</param>
        /// <param name="fieldName">字段名</param>
        /// <returns>字段值</returns>
        /// <exception cref="ArgumentNullException">当typeName或fieldName为null时抛出</exception>
        /// <exception cref="TypeLoadException">当无法找到指定类型时抛出</exception>
        /// <exception cref="ArgumentException">当无法找到指定字段时抛出</exception>
        /// <exception cref="InvalidCastException">当字段值无法转换为指定类型时抛出</exception>
        public static T GetStaticField<T>(string typeName, string fieldName)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            Type type = Type.GetType(typeName);
            if (type == null)
                throw new TypeLoadException($"无法找到类型: {typeName}");

            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new ArgumentException($"无法找到静态字段: {typeName}.{fieldName}");

            var value = field.GetValue(null);
            if (value == null && !typeof(T).IsValueType)
                return default(T);

            try
            {
                return (T)value;
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException($"无法将字段值从 {value?.GetType()?.Name ?? "null"} 转换为 {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 获取静态属性的值
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="typeName">类型全名</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性值</returns>
        /// <exception cref="ArgumentNullException">当typeName或propertyName为null时抛出</exception>
        /// <exception cref="TypeLoadException">当无法找到指定类型时抛出</exception>
        /// <exception cref="ArgumentException">当无法找到指定属性时抛出</exception>
        /// <exception cref="InvalidCastException">当属性值无法转换为指定类型时抛出</exception>
        public static T GetStaticProperty<T>(string typeName, string propertyName)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName));
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            Type type = Type.GetType(typeName);
            if (type == null)
                throw new TypeLoadException($"无法找到类型: {typeName}");

            var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                throw new ArgumentException($"无法找到静态属性: {typeName}.{propertyName}");

            var value = property.GetValue(null);
            if (value == null && !typeof(T).IsValueType)
                return default(T);

            try
            {
                return (T)value;
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException($"无法将属性值从 {value?.GetType()?.Name ?? "null"} 转换为 {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 获取枚举值的Description特性描述
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="self">枚举值</param>
        /// <returns>Description特性的描述文本，如果没有则返回枚举值的字符串表示</returns>
        public static string GetDescription<T>(this T self) where T : Enum
        {
            var type = typeof(T);
            var memberInfo = type.GetMember(self.ToString());
            if (memberInfo.Length > 0)
            {
                var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0)
                    return ((DescriptionAttribute)attributes[0]).Description;
            }
            return self.ToString();
        }

        /// <summary>
        /// 类型收集标志枚举
        /// </summary>
        [Flags]
        public enum ECollectFlags
        {
            /// <summary>
            /// 收集类
            /// </summary>
            Class = 1 << 0,
            
            /// <summary>
            /// 收集接口
            /// </summary>
            Interface = 1 << 1,

            /// <summary>
            /// 包含抽象类型
            /// </summary>
            IncludeAbstract = 1 << 2,
            
            /// <summary>
            /// 只收集抽象类型
            /// </summary>
            OnlyAbstract = 1 << 3,

            /// <summary>
            /// 收集所有类型（类、接口、包含抽象）
            /// </summary>
            All = Class | Interface | IncludeAbstract
        }

        /// <summary>
        /// 获取所有继承自指定类型的类型（从所有程序集中搜索）
        /// </summary>
        /// <typeparam name="T">基类型</typeparam>
        /// <param name="collectFlags">收集标志</param>
        /// <returns>符合条件的类型列表</returns>
        public static List<Type> GetAllInheritedTypes<T>(ECollectFlags collectFlags)
        {
            List<Type> result = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = GetAllInheritedTypes<T>(assembly, collectFlags);
                    result.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 某些程序集可能无法加载所有类型，记录警告但继续处理
#if !GAME_SERVER
                    UnityEngine.Debug.LogWarning($"[ReflectionHelper] 跳过程序集 {assembly.FullName}: {ex.Message}");
#endif
                }
                catch (Exception ex)
                {
#if !GAME_SERVER
                    UnityEngine.Debug.LogWarning($"[ReflectionHelper] 处理程序集 {assembly.FullName} 时出错: {ex.Message}");
#endif
                }
            }
            
            return result;
        }

        /// <summary>
        /// 获取指定程序集中所有继承自指定类型的类型
        /// </summary>
        /// <typeparam name="T">基类型</typeparam>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="collectFlags">收集标志</param>
        /// <returns>符合条件的类型列表</returns>
        /// <exception cref="ArgumentNullException">当assemblyName为null时抛出</exception>
        /// <exception cref="FileNotFoundException">当无法找到指定程序集时抛出</exception>
        public static List<Type> GetAllInheritedTypes<T>(string assemblyName, ECollectFlags collectFlags)
        {
            if (string.IsNullOrEmpty(assemblyName))
                throw new ArgumentNullException(nameof(assemblyName));

            try
            {
                var assembly = Assembly.Load(assemblyName);
                return GetAllInheritedTypes<T>(assembly, collectFlags);
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException || ex is BadImageFormatException)
            {
                throw new System.IO.FileNotFoundException($"无法加载程序集: {assemblyName}", ex);
            }
        }

        /// <summary>
        /// 从指定程序集中获取所有可分配给指定类型的类型
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="assembly">要搜索的程序集</param>
        /// <param name="collectFlags">收集标志</param>
        /// <returns>符合条件的类型列表</returns>
        /// <exception cref="ArgumentNullException">当assembly为null时抛出</exception>
        public static List<Type> GetAllInheritedTypes<T>(Assembly assembly, ECollectFlags collectFlags)
        {
            var rootType = typeof(T);
            return assembly.GetTypes().AsValueEnumerable()
                .Where(IsSuitable)
                .ToList();

            bool IsSuitable(Type typeInAssembly)
            {
                // 修复逻辑错误：应该检查typeInAssembly是否可以分配给rootType
                if (!rootType.IsAssignableFrom(typeInAssembly))
                    return false;

                // 检查类型过滤条件
                bool isClass = typeInAssembly.IsClass && !typeInAssembly.IsInterface;
                bool isInterface = typeInAssembly.IsInterface;
                
                if (collectFlags.HasFlag(ECollectFlags.Class) && collectFlags.HasFlag(ECollectFlags.Interface))
                {
                    // 如果同时指定了Class和Interface，则接受两者
                    if (!isClass && !isInterface)
                        return false;
                }
                else if (collectFlags.HasFlag(ECollectFlags.Class))
                {
                    if (!isClass)
                        return false;
                }
                else if (collectFlags.HasFlag(ECollectFlags.Interface))
                {
                    if (!isInterface)
                        return false;
                }

                // 检查抽象类型过滤条件
                if (collectFlags.HasFlag(ECollectFlags.OnlyAbstract))
                {
                    if (!typeInAssembly.IsAbstract)
                        return false;
                }
                else if (!collectFlags.HasFlag(ECollectFlags.IncludeAbstract))
                {
                    if (typeInAssembly.IsAbstract)
                        return false;
                }

                return true;
            }
        }
    }
}