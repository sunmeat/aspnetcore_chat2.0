using Chat.Hubs;
using Chat.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = null; // імена властивостей без camelCase, як в класі-моделі
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // видаляємо базу, якщо вона існує (якщо законментувати, то історія повідомлень збережеться при перезапусках)
    db.Database.EnsureDeleted();
    // створюємо базу і таблицю автоматично за моделлю ChatMessage
    db.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Login}/{id?}");

// відкрити декілька вкладок браузера одночасно за шляхом https://localhost:7260/Chat/Login
// кожна вкладка буде окремим користувачем із власним іменем
app.MapHub<ChatHub>("/chatHub");

app.Run();