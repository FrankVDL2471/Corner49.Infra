# Introduction 
Corner49.Infra is an opinionated libray handle some infrastructure components for .Net projects

# Sample Program.cs



```cs
using Corner49.Infra;

var infra = WebApplication.CreateBuilder(args)
	.UseInfra("Sample")
	.WithViewControllers();


//Add Custom services here


//Build and run
await infra.BuildAndRun();
```

