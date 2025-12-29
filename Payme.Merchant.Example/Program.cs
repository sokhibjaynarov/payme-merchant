using Payme.Merchant.Abstractions;
using Payme.Merchant.Example.Services;
using Payme.Merchant.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Payme
builder.Services.AddPaymeMerchant(options =>
{
    options.Login = "Paycom";
    options.Password = "SECRET_KEY"; // Replace with real key
});

// Register Business Logic
builder.Services.AddSingleton<InMemoryPaymeService>();
builder.Services.AddSingleton<IPaymeOrderService>(sp => sp.GetRequiredService<InMemoryPaymeService>());
builder.Services.AddSingleton<IPaymeTransactionStore>(sp => sp.GetRequiredService<InMemoryPaymeService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
