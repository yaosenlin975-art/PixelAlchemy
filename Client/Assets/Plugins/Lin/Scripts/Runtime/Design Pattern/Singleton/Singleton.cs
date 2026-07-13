/*
┌────────────────────────────┐
│　Description: 一般类单例
│　Author: 花球i
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: Singleton
└──────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.DesignPattern.Singleton
{
    public abstract class Singleton<T> where T : Singleton<T>, new()
    {
        private static T instance;

        public static T GetInstance()
        {
            if (instance == null)
            {
                instance = new T();
                Application.quitting += Application_quitting;
            }
            return instance;
        }

        private static void Application_quitting()
        {
            instance = null;
            Application.quitting -= Application_quitting;
        }
    }
}
