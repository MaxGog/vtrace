using Microsoft.Extensions.Logging;
using vtrace.Convecters;
using vtrace.Interfaces;
using vtrace.Services;
using vtrace.ViewModels;
using vtrace.Views;

namespace vtrace;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

			builder.Services.AddSingleton<IConfigStorageService, JsonFileConfigStorageService>();
			builder.Services.AddSingleton<IVlessVpnService, VlessVpnService>();
			
			builder.Services.AddTransient<VpnConfigViewModel>();
			
			builder.Services.AddTransient<VpnConfigPage>();
			
			builder.Services.AddSingleton<IValueConverter, BoolToConnectTextConverter>();
			builder.Services.AddSingleton<IValueConverter, BoolToColorConverter>();
			builder.Services.AddSingleton<IValueConverter, AnyItemConnectedConverter>();
			builder.Services.AddSingleton<IValueConverter, InverseBooleanConverter>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
