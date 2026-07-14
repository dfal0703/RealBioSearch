using System;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;

namespace Haare.Client.Core.Singleton
{
    public abstract class Singleton<T> : NativeRoutine where T : new()
    {		
        
        /// <summary>
        /// is already Created?
        /// </summary>
        public static bool isCreated => instance != null;
        
        private static T instance;
        public static T Instance
        {
            get
            {
                if ( !isCreated )	{ Create(); }
                return instance;
            }
        }
        
        
        static protected void Create() {
            if ( isCreated )	{ return; }
            Console.WriteLine($"Singleton Generated {typeof(T).ToString()}");
            instance = new T();
        }
        
        static public async UniTask WaitForCreation() {
            
            var i = Instance;
            await UniTask.Delay( 1 );
        }
        
        
    }
}