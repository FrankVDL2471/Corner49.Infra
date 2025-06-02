# Introduction 
Corner49.FormBuilder is a dynamic form builder for .Net Core MVC apps


# How to use

```cshtml
@using Corner49.FormBuilder
@using Corner49.Sample.Models
@model DataModel

@{
    ViewData["Title"] = "My Form";
}


@Html.BuildForm(Model).Build()

```

