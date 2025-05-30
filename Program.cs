using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

var users = new List<User>
{
    new(1, "Alice", "alice@example.com"),
    new(2, "Bob", "bob@example.com")
};

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string validToken = "my-secret-token";

// Error-handling middleware (first)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
    }
});

// Authentication middleware (second)
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (authHeader == null || !authHeader.StartsWith("Bearer ") || authHeader.Split(" ")[1] != validToken)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Invalid or missing token." });
        return;
    }

    await next();
});

// Logging middleware (third)
app.Use(async (context, next) =>
{
    await next();
    Console.WriteLine($"[LOG] {context.Request.Method} {context.Request.Path} => {context.Response.StatusCode}");
});

// GET /api/users (pagination support)
app.MapGet("/api/users", (int page = 1, int pageSize = 10) =>
{
    var paginatedUsers = users.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    return Results.Ok(new
    {
        TotalUsers = users.Count,
        CurrentPage = page,
        PageSize = pageSize,
        Users = paginatedUsers
    });
});

// GET /api/users/{id}
app.MapGet("/api/users/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound(new { Message = "User not found" });
});

// POST /api/users (validation included)
app.MapPost("/api/users", (User user) =>
{
    var validationResults = ValidateUser(user);
    if (validationResults.Any()) return Results.BadRequest(validationResults);

    user.Id = users.Any() ? users.Max(u => u.Id) + 1 : 1;
    users.Add(user);
    return Results.Created($"/api/users/{user.Id}", user);
});

// PUT /api/users/{id} (validation included)
app.MapPut("/api/users/{id}", (int id, User updatedUser) =>
{
    var validationResults = ValidateUser(updatedUser);
    if (validationResults.Any()) return Results.BadRequest(validationResults);

    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound(new { Message = "User not found" });

    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    return Results.NoContent();
});

// DELETE /api/users/{id}
app.MapDelete("/api/users/{id}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound(new { Message = "User not found" });

    users.Remove(user);
    return Results.NoContent();
});

app.Run();

// User record with validation attributes
record User(int Id, [Required, MinLength(2)] string Name, [Required, EmailAddress] string Email);

// Helper function for manual validation
List<object> ValidateUser(User user)
{
    var context = new ValidationContext(user);
    var results = new List<ValidationResult>();
    var errors = new List<object>();

    if (!Validator.TryValidateObject(user, context, results, true))
    {
        foreach (var result in results)
        {
            errors.Add(new { Field = result.MemberNames.FirstOrDefault(), Error = result.ErrorMessage });
        }
    }

    return errors;
}
