using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── 认证与授权 ────────────────────────────────────────────────────────
// 必须注册 AddAuthentication / AddAuthorization，否则下面的
// app.UseAuthorization() 会静默失效：接口打上 [Authorize] 后既不报错、
// 也不生效，任何人都能调用。这里在启动时就把配置校验掉，宁可起不来
// 也不要带着一个假的权限校验跑起来。
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "缺少配置 Jwt:Key，或密钥长度不足 32 字节（HMAC-SHA256 要求）。" +
        "本地开发检查 appsettings.Development.json；" +
        "生产环境请改用环境变量或 User Secrets 注入，不要写进配置文件。");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("缺少配置 Jwt:Issuer 或 Jwt:Audience。");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            // 默认允许 5 分钟时钟偏移，对内部系统太长，容易让已过期的 Token 继续可用
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 顺序不能颠倒：先认证（确认你是谁），再授权（确认你能不能做）
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
