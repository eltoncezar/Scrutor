# Scrutor [![Build status](https://ci.appveyor.com/api/projects/status/j00uyvqnm54rdlkb?svg=true)](https://ci.appveyor.com/project/khellang/scrutor) [![NuGet Package](https://img.shields.io/nuget/v/Scrutor.svg)](https://www.nuget.org/packages/Scrutor)

> Scrutor - I search or examine thoroughly; I probe, investigate or scrutinize  
> From scrūta, as the original sense of the verb was to search through trash. - https://en.wiktionary.org/wiki/scrutor

[Assembly scanning](#assembly-scanning) and [decoration extensions]() for Microsoft.Extensions.DependencyInjection

## Installation

Install the [Scrutor NuGet Package](https://www.nuget.org/packages/Scrutor).

### Package Manager Console

```
Install-Package Scrutor
```

### .NET Core CLI

```
dotnet add package Scrutor
```

## Usage

The library adds two extension methods to `IServiceCollection`:

* `Scan` - This is the entry point to set up your assembly scanning.
* `Decorate` - This method is used to decorate already registered services.

See **Examples** below for usage examples.

## Examples

### Scanning

```csharp
var collection = new ServiceCollection();

collection.Scan(scan => scan
     // We start out with all types in the assembly of ITransientService
    .FromAssemblyOf<ITransientService>()
        // AddClasses starts out with all public, non-abstract types in this assembly.
        // These types are then filtered by the delegate passed to the method.
        // In this case, we filter out only the classes that are assignable to ITransientService.
        .AddClasses(classes => classes.AssignableTo<ITransientService>())
            // We then specify what type we want to register these classes as.
            // In this case, we want to register the types as all of its implemented interfaces.
            // So if a type implements 3 interfaces; A, B, C, we'd end up with three separate registrations.
            .AsImplementedInterfaces()
            // And lastly, we specify the lifetime of these registrations.
            .WithTransientLifetime()
        // Here we start again, with a new full set of classes from the assembly above.
        // This time, filtering out only the classes assignable to IScopedService.
        .AddClasses(classes => classes.AssignableTo<IScopedService>())
            // Now, we just want to register these types as a single interface, IScopedService.
            .As<IScopedService>()
            // And again, just specify the lifetime.
            .WithScopedLifetime()
        // Generic interfaces are also supported too, e.g. public interface IOpenGeneric<T> 
        .AddClasses(classes => classes.AssignableTo(typeof(IOpenGeneric<>)))
            .AsImplementedInterfaces()
        // And you scan generics with multiple type parameters too
        // e.g. public interface IQueryHandler<TQuery, TResult>
        .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)))
            .AsImplementedInterfaces());
```

### Decoration

```csharp
var collection = new ServiceCollection();

// First, add our service to the collection.
collection.AddSingleton<IDecoratedService, Decorated>();

// Then, decorate Decorated with the Decorator type.
collection.Decorate<IDecoratedService, Decorator>();

// Finally, decorate Decorator with the OtherDecorator type.
// As you can see, OtherDecorator requires a separate service, IService. We can get that from the provider argument.
collection.Decorate<IDecoratedService>((inner, provider) => new OtherDecorator(inner, provider.GetRequiredService<IService>()));

var serviceProvider = collection.BuildServiceProvider();

// When we resolve the IDecoratedService service, we'll get the following structure:
// OtherDecorator -> Decorator -> Decorated
var instance = serviceProvider.GetRequiredService<IDecoratedService>();
```

# Documentation

[Andrew Lock](https://andrewlock.net/) has two great blog posts that go further into detail on Scrutor. This documentation is based on those. Go check them:

- [Using Scrutor to automatically register your services with the ASP.NET Core DI container](https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/)

- [Adding decorated classes to the ASP.NET Core DI container using Scrutor](https://andrewlock.net/adding-decorated-classes-to-the-asp.net-core-di-container-using-scrutor/)

## What Scrutor it is and what is not

Scrutor is:

- An extension for `IServiceCollection`
- An easier way to register your services, through assembly scanning and service decorator

Scrutor is not:

- A new DI container
- A full featured suite of extensions for Microsoft.Extensions.DependencyInjection
- A replacement for a full featured DI Container

Using Scrutor you can turn this:

```csharp
services.AddScoped<IFoo, Foo>();
services.AddScoped<IBar, Bar>();
services.AddScoped<IBaz, Baz>();
```

Into this:

```csharp
services.Scan(scan => 
    scan.FromCallingAssembly()                    
        .AddClasses()
        .AsMatchingInterface());
```

## Assembly Scanning

The `Scan()` method receives a configuration action, in which you define four things:

1. [*A selector*](#1-a-selector) - which implementations (concrete classes) to register
2. [*A registration strategy*](#2-a-registration-strategy) - how to handle duplicate services or implementations (optional)
3. [*The services*](#3-the-services) - which services (i.e. interfaces) each implementation should be registered as
4. [*The lifetime*](#4-the-lifetime) - what lifetime to use for the registrations

```csharp
services.Scan(scan => scan     
  .FromCallingAssembly()                                    // 1. Find the concrete classes
    .AddClasses()                                           //    to register
      .UsingRegistrationStrategy(RegistrationStrategy.Skip) // 2. Define how to handle duplicates
      .AsSelf()                                             // 3. Specify which services they are registered as
      .WithTransientLifetime());                            // 4. Set the lifetime for the services
```

### 1. A selector

#### Specifying the types explicitly

```csharp
services.Scan(scan => scan
  .AddTypes<Service1, Service2>()
    .AsSelf()
    .WithTransientLifetime());
```

#### Scanning an assembly for types

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .AsSelf()
      .WithTransientLifetime());
```

The following scanning methods are provided:

- `FromAssemblyOf<>`, `FromAssembliesOf` - Scan the assemblies containing the provided `Type` or `Type`s
- `FromCallingAssembly`, `FromExecutingAssembly`, `FromEntryAssembly` - Scan the calling, executing, or entry assembly. See [the `Assembly` static methods](https://docs.microsoft.com/dotnet/api/system.reflection.assembly?view=netstandard-2.0) for details on the differences between them.
- `FromAssemblyDependencies` - Scan all assemblies that the provided `Assembly` depends on.
- `FromApplicationDependencies`, `FromDependencyContext` - Scan runtime libraries.

#### Filtering the classes you find

Whichever assembly scanning approach you choose, you need to call `AddClasses()` afterwards, to select the concrete types to add to the container. This method has several overloads you can use to filter out which classes are selected:

- `AddClasses()` - Add all public, non-abstract classes.
- `AddClasses(publicOnly)` - Add all non-abstract classes. Set `publicOnly=false` to add `internal`/`private` nested classes too.
- `AddClass(predicate)` - Run an arbitrary action to filter which classes include. This is very useful and used extensively, as shown below.
- `AddClasses(predicate, publicOnly)` - A combination of the previous two methods.

The ability to run a predicate for every concrete class discovered is very useful. You can use this predicate in many different ways. For example, to only include classes which can be assigned to (i.e. implement) a specific interface, you could do:

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses(classes => classes.AssignableTo<IService>())
        .AsImplementedInterfaces()
        .WithTransientLifetime());
```

Or you could restrict to only those classes in a specific namespace:

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses(classes => classes.InNamespaces("MyApp"))
        .AsImplementedInterfaces()
        .WithTransientLifetime());
```

Alternatively, you can use an arbitrary filter based on the `Type` itself:

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Repository"))
        .AsImplementedInterfaces()
        .WithTransientLifetime());
```

### 2. A registration strategy

Scrutor lets you control how to handle the case where a service has already been registered in the DI container by specifying a `ReplacementStrategy`. There are currently five different replacement strategies you can use:

- `Append` - Don't worry about duplicate registrations, add new registrations for existing services. *This is the default behaviour if you don't specify a registration strategy.*
- `Skip` - If the service is already registered in the container, don't add a new registration.
- `Replace(ReplacementBehavior.ServiceType)` - If the *service* is already registered in the container, remove all previous registrations for that *service* before creating a new registration.
- `Replace(ReplacementBehavior.ImplementationType)` - If the *implementation* is already registered in the container, remove all previous registrations where the *implementation* matches the new registration, before creating a new registration.
- `Replace(ReplacementBehavior.All)` - Apply both of the previous behaviours. If the *service or the implementation* have previously been registered, remove all of those registrations first.

### 3. The services

Let's see the different registration methods and their equivalent "manual" registration, assuming we have the following class in our assembly scanning:

```csharp
public class TestService: ITestService, IService {}
```

#### `AsSelf()`

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .AsSelf()
      .WithSingletonLifetime());
```

Equivalent to:

```csharp
services.AddSingleton<TestService>();
```

#### `AsMatchingInterface()`

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .AsMatchingInterface()
      .WithSingletonLifetime());
```

Equivalent to:

```csharp
services.AddSingleton<ITestService, TestService>();
```

#### `AsImplementedInterfaces()`

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .AsImplementedInterfaces()
      .WithSingletonLifetime());
```

Equivalent to:

```csharp
services.AddSingleton<ITestService, TestService>();
services.AddSingleton<IService, TestService>();
```

#### `AsSelfWithInterfaces()`

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .AsSelfWithInterfaces()
      .WithSingletonLifetime());
```

Equivalent to:

```csharp
services.AddSingleton<TestService>();
services.AddSingleton<ITestService>(x => x.GetRequiredService<TestService>());
services.AddSingleton<IService>(x => x.GetRequiredService<TestService>());
```

#### As<>()

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<IService>()
    .AddClasses()
      .As<IMyService>()
      .WithSingletonLifetime());
```

Equivalent to:

```csharp
services.AddSingleton<IMyService, TestService>();
```

### 4. The lifetime

Scrutor has methods that correspond to the three lifetimes in ASP.NET Core:

- `WithTransientLifetime()` - Transient is the default lifetime if you don't specify one.
- `WithScopedLifetime()` - Use the same service scoped to the lifetime of a request.
- `WithSingletonLifetime()` - Use a single instance of the service for the lifetime of the app.

## Chaining multiple selectors together

Scrutor's API allows you to chain together multiple scans of an assembly, specifying the rules for a subset of classes at a time.

```csharp
services.Scan(scan => scan
  .FromAssemblyOf<CombinedService>()
    .AddClasses(classes => classes.AssignableTo<ICombinedService>()) // Filter classes
      .AsSelfWithInterfaces()
      .WithSingletonLifetime()

    .AddClasses(x=> x.AssignableTo(typeof(IOpenGeneric<>))) // Can close generic types
      .AsMatchingInterface()

    .AddClasses(x=> x.InNamespaceOf<MyClass>())
      .UsingRegistrationStrategy(RegistrationStrategy.Replace()) // Defaults to ReplacementBehavior.ServiceType
      .AsMatchingInterface()
      .WithScopedLifetime()

  .FromAssemblyOf<DatabaseContext>()   // Can load from multiple assemblies within one Scan()
    .AddClasses() 
      .AsImplementedInterfaces()
);
```
