var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg => {
    // Wskazujemy assembly (projekt), w którym znajduj¹ siê Twoje handlery.
    // Jeœli handlery s¹ w tym samym projekcie co Program.cs:
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

    // Jeœli handlery trzymasz w osobnym projekcie (np. Core), wska¿ dowoln¹ klasê z tamtego projektu:
    // cfg.RegisterServicesFromAssembly(typeof(CreateUserCommand).Assembly); 
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
