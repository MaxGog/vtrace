using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
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
        	.UseSkiaSharp()
        	.UseLiveCharts()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		ConfigureServices(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		var configFilePath = Path.Combine(FileSystem.AppDataDirectory, "vless_configs.json");

		services.AddSingleton<IConfigStorageService>(_ =>
			new JsonFileConfigStorageService(configFilePath));

		services.AddSingleton<IVlessVpnService, VlessVpnService>();

		services.AddTransient<VpnConfigViewModel>();

		services.AddTransient<VpnConfigPage>();

		services.AddSingleton<BoolToConnectTextConverter>();
		services.AddSingleton<BoolToColorConverter>();
		services.AddSingleton<AnyItemConnectedConverter>();
		services.AddSingleton<InverseBooleanConverter>();

		services.AddSingleton<IValueConverter, BoolToConnectTextConverter>();
		services.AddSingleton<IValueConverter, BoolToColorConverter>();
		services.AddSingleton<IValueConverter, AnyItemConnectedConverter>();
		services.AddSingleton<IValueConverter, InverseBooleanConverter>();
	}

}