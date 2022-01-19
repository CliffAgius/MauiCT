﻿using System;
using System.Collections.Generic;

/* Unmerged change from project 'CommunityToolkit.Maui (net6.0-maccatalyst)'
Before:
#if __IOS__ || MACCATALYST
After:
using Microsoft;
using Microsoft.Maui;
using Microsoft.Maui.Platform;
using CommunityToolkit.Maui.Extensions.Workarounds;
#if __IOS__ || MACCATALYST
*/
using Microsoft.Maui.Platform;
#if __IOS__ || MACCATALYST
using NativeView = UIKit.UIView;
using BasePlatformType = Foundation.NSObject;
using PlatformWindow = UIKit.UIWindow;
using PlatformApplication = UIKit.UIApplicationDelegate;
#elif ANDROID
using NativeView = Android.Views.View;
using BasePlatformType = Android.Content.Context;
using PlatformWindow = Android.App.Activity;
using PlatformApplication = Android.App.Application;
#elif WINDOWS
using NativeView = Microsoft.UI.Xaml.FrameworkElement;
using BasePlatformType = WinRT.IWinRTObject;
using PlatformWindow = Microsoft.UI.Xaml.Window;
using PlatformApplication = Microsoft.UI.Xaml.Application;
#elif NETSTANDARD || (NET6_0 && !IOS && !ANDROID)
using NativeView = System.Object;
using BasePlatformType = System.Object;
using INativeViewHandler = Microsoft.Maui.IViewHandler;
using PlatformWindow = System.Object;
using PlatformApplication = System.Object;
#endif

namespace CommunityToolkit.Core.Extensions.Workarounds;

public static partial class ElementExtensions
{
	static HashSet<Type> handlersWithConstructors = new HashSet<Type>();

	static IElementHandler? CreateTypeWithInjection(this Type viewType, IMauiContext mauiContext)
	{
		var handlerType = mauiContext.Handlers.GetHandlerType(viewType);

		if (handlerType == null)
		{
			return null;
		}

		return (IElementHandler)Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(mauiContext.Services, handlerType, mauiContext);
	}

	public static IElementHandler ToHandlerMCT(this IElement view, IMauiContext context)
	{
		_ = view ?? throw new ArgumentNullException(nameof(view));
		_ = context ?? throw new ArgumentNullException(nameof(context));

		//This is how MVU works. It collapses views down
		if (view is IReplaceableView ir)
		{
			view = ir.ReplacedView;
		}

		var handler = view.Handler;

		if (handler?.MauiContext != null && handler.MauiContext != context)
		{
			handler = null;
		}


		// TODO Clean up this handler create. Handlers should probably create through the 
		// DI.Ext Service provider. We just register them all as transient? possibly?
		if (handler == null)
		{
			var viewType = view.GetType();
			try
			{
				if (handlersWithConstructors.Contains(viewType))
				{
					handler = viewType.CreateTypeWithInjection(context);
				}
				else
				{
					handler = context.Handlers.GetHandler(viewType);
				}
			}
			catch (MissingMethodException)
			{
				handler = viewType.CreateTypeWithInjection(context);
				if (handler != null)
				{
					handlersWithConstructors.Add(view.GetType());
				}
			}
		}

		if (handler == null)
		{
			throw new Exception($"Handler not found for view {view}.");
		}

		handler.SetMauiContext(context);

		view.Handler = handler;

		if (handler.VirtualView != view)
		{
			handler.SetVirtualView(view);
		}

		return handler;
	}

	static NativeView? ToNative(this IElement view)
	{
		if (view is IReplaceableView replaceableView && replaceableView.ReplacedView != view)
		{
			return replaceableView.ReplacedView.ToNative();
		}

		if (view.Handler == null)
		{
			var mauiContext = view.Parent?.Handler?.MauiContext ??
				throw new InvalidOperationException($"{nameof(MauiContext)} should have been set on parent.");

			return view.ToNative(mauiContext);
		}

		if (view.Handler is INativeViewHandler nativeHandler && nativeHandler.NativeView != null)
		{
			return nativeHandler.NativeView;
		}

		return view.Handler?.NativeView as NativeView;

	}

	static NativeView ToNative(this IElement view, IMauiContext context)
	{
		var handler = view.ToHandlerMCT(context);

		if (handler.NativeView is not NativeView result)
		{
			throw new InvalidOperationException($"Unable to convert {view} to {typeof(NativeView)}");
		}

		return view.ToNative() ?? throw new InvalidOperationException($"Unable to convert {view} to {typeof(NativeView)}");

	}

	static void SetHandler(this BasePlatformType nativeElement, IElement element, IMauiContext context)
	{
		_ = nativeElement ?? throw new ArgumentNullException(nameof(nativeElement));
		_ = element ?? throw new ArgumentNullException(nameof(element));
		_ = context ?? throw new ArgumentNullException(nameof(context));

		var handler = element.Handler;
		if (handler?.MauiContext != null && handler.MauiContext != context)
		{
			handler = null;
		}

		if (handler == null)
		{
			handler = context.Handlers.GetHandler(element.GetType());
		}

		if (handler == null)
		{
			throw new Exception($"Handler not found for window {element}.");
		}

		handler.SetMauiContext(context);

		element.Handler = handler;

		if (handler.VirtualView != element)
		{
			handler.SetVirtualView(element);
		}
	}

	static void SetApplicationHandler(this PlatformApplication nativeApplication, IApplication application, IMauiContext context) =>
		nativeApplication.SetHandler(application, context);

	static void SetWindowHandler(this PlatformWindow nativeWindow, IWindow window, IMauiContext context) =>
		nativeWindow.SetHandler(window, context);
}