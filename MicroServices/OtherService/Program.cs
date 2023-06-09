using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Filters;

//cors variable
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

var builder = WebApplication.CreateBuilder(args);

var handler = new HttpClientHandler
{
    UseProxy = true,
    // Autres configurations de proxy
};

builder.Services.AddSingleton(new HttpClient(handler));

//DB context
// var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
// var dbName = Environment.GetEnvironmentVariable("DB_NAME");
// var dbPassword = Environment.GetEnvironmentVariable("DB_MSSQL_SA_PASSWORD");
// var _connStr = $"Server={dbHost};Database={dbName};User Id=SA;Password={dbPassword};TrustServerCertificate=True;Trusted_Connection=true;";

// builder.Services.AddDbContext<DataContext>(options =>
//     options.UseSqlServer(_connStr));

builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

//swagger configuration
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Description = """Standard Authorization header using the Bearer scheme. Example : "bearer {token}" """,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });

    c.OperationFilter<SecurityRequirementsOperationFilter>();
});

//AUTO MAPPER
builder.Services.AddAutoMapper(typeof(Program).Assembly);

//SERVICES
builder.Services.AddScoped<IUserService, Services.UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ICryptService, CryptService>();

builder.Services.AddHttpClient();

//authentification middleware
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

//CORS POLICY
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy  =>
            {
                //authorize access from api gateway
                policy.AllowAnyOrigin();
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
                policy.AllowCredentials();
            });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/userservice"), appBuilder =>
{
    appBuilder.UseCookieVerification();
    appBuilder.UseJWTVerification();
});


app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// REVIEW: This is done fore development east but shouldn't be here in production
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    //var logger = app.Services.GetService<ILogger<DataContextSeed>>();
    await context.Database.MigrateAsync();

    //await new DataContextSeed().SeedAsync(context);
    // await integEventContext.Database.MigrateAsync();
}

app.Run();