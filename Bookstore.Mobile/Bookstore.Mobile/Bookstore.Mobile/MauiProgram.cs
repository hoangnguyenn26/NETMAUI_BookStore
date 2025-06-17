var apiSettings = new ApiSettings();
var platform = DeviceInfo.Platform.ToString();
var baseAddress = configuration[$"ApiSettings:{platform}:BaseAddress"] ?? configuration["ApiSettings:BaseAddress"] ?? "https://localhost:7264/api";
apiSettings.BaseAddress = baseAddress;

builder.Services.AddSingleton(apiSettings); 