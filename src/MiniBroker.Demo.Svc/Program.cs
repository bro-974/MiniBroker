using MiniBroker.Worker;

var builder = WebApplication.CreateBuilder(args);

//add api
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add grpc.
builder.Services.AddGrpc();
builder.Services.RegisterMiniBroker();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline.
app.MapMiniBroker();
//app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();