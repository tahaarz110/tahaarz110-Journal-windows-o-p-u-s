// ابتدای فایل: Services/ServiceLocator.cs
// مسیر: /Services/ServiceLocator.cs

using System;
using System.Collections.Generic;

namespace TradingJournal.Services
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static readonly Dictionary<Type, Type> _serviceTypes = new();

        public static void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public static void Register<TInterface, TImplementation>() 
            where TImplementation : TInterface, new()
        {
            _serviceTypes[typeof(TInterface)] = typeof(TImplementation);
        }

        public static T GetService<T>() where T : class
        {
            var type = typeof(T);
            
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            
            if (_serviceTypes.TryGetValue(type, out var implementationType))
            {
                var instance = Activator.CreateInstance(implementationType);
                _services[type] = instance!;
                return (T)instance;
            }
            
            throw new InvalidOperationException($"Service {type.Name} not registered");
        }

        public static void Clear()
        {
            _services.Clear();
            _serviceTypes.Clear();
        }
    }
}

// پایان فایل: Services/ServiceLocator.cs