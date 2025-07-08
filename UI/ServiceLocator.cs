using System;
using Microsoft.Extensions.DependencyInjection;

namespace UI;

public static class ServiceLocator
{
    private static IServiceProvider _serviceProvider;
    
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public static T GetService<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider has not been initialized.");
        }
            
        return _serviceProvider.GetService<T>();
    }
    
    public static T GetRequiredService<T>() where T : class
    {
        var service = GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T)} not found.");
        }
        return service;
    }
    
    public static object GetRequiredService(Type type)
    {
        var service = _serviceProvider.GetService(type);
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {type} not found.");
        }
        return service;
    }
}