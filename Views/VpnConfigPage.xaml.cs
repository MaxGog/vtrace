using System.Diagnostics;
using vtrace.Models;
using vtrace.ViewModels;

namespace vtrace.Views;

public partial class VpnConfigPage : ContentPage
{

	public VpnConfigPage(VpnConfigViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

}
