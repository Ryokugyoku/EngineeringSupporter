using EngineeringSupporter.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace EngineeringSupporter;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ApplyMigrations();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    private static void ApplyMigrations()
    {
        var services = Current?.Handler?.MauiContext?.Services;
        if (services is null)
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!dbContext.Database.GetMigrations().Any())
        {
            dbContext.Database.EnsureCreated();
            return;
        }

        dbContext.Database.Migrate();
    }
}
