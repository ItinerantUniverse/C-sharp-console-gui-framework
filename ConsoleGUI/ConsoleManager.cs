using ConsoleGUI.Api;
using ConsoleGUI.Buffering;
using ConsoleGUI.Common;
using ConsoleGUI.Controls;
using ConsoleGUI.Data;
using ConsoleGUI.Input;
using ConsoleGUI.Space;
using ConsoleGUI.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ConsoleGUI
{
	public class ConsoleManager
	{
		private class ConsoleManagerDrawingContextListener : IDrawingContextListener
		{
			private readonly ConsoleManager _consoleManager;

			public ConsoleManagerDrawingContextListener(ConsoleManager consoleManager)
			{
				_consoleManager = consoleManager;
			}

			void IDrawingContextListener.OnRedraw(DrawingContext drawingContext)
			{
				if (_consoleManager.FreezeLock.IsFrozen) return;
				_consoleManager.Redraw();
			}

			void IDrawingContextListener.OnUpdate(DrawingContext drawingContext, Rect rect)
			{
				if (_consoleManager.FreezeLock.IsFrozen) return;
                _consoleManager.Update(rect);
			}
		}

		private readonly ConsoleBuffer _buffer = new ConsoleBuffer();
		private FreezeLock _freezeLock;
		internal FreezeLock FreezeLock => _freezeLock;

		private DrawingContext _contentContext = DrawingContext.Dummy;
		private DrawingContext ContentContext
		{
			get => _contentContext;
			set => Setter
				.SetDisposable(ref _contentContext, value)
				.Then(Initialize);
		}

		private IControl _content;
		public IControl Content
		{
			get => _content;
			set => Setter
				.Set(ref _content, value)
				.Then(BindContent);
		}

		private IConsole _console = new StandardConsole();
		public IConsole Console
		{
			get => _console;
			set => Setter
				.Set(ref _console, value)
				.Then(Initialize);
		}

		private Position? _mousePosition;
		public Position? MousePosition
		{
			get => _mousePosition;
			set => Setter
				.Set(ref _mousePosition, value)
				.Then(UpdateMouseContext);
		}

		private bool _mouseDown;
		public bool MouseDown
		{
			get => _mouseDown;
			set
			{
				if (_mouseDown && !value)
					MouseContext?.MouseListener?.OnMouseUp(MouseContext.Value.RelativePosition);
				if (!_mouseDown && value)
					MouseContext?.MouseListener?.OnMouseDown(MouseContext.Value.RelativePosition);

				_mouseDown = value;
			}
		}

		private MouseContext? _mouseContext;
		private MouseContext? MouseContext
		{
			get => _mouseContext;
			set
			{
				if (value?.MouseListener != _mouseContext?.MouseListener)
				{
					_mouseContext?.MouseListener.OnMouseLeave();
					value?.MouseListener.OnMouseEnter();
					value?.MouseListener.OnMouseMove(value.Value.RelativePosition);
				}
				else if (value.HasValue && value.Value.RelativePosition != _mouseContext?.RelativePosition)
				{
					value.Value.MouseListener.OnMouseMove(value.Value.RelativePosition);
				}

				_mouseContext = value;
			}
		}

		public Size WindowSize => Console.Size;
		public Size BufferSize => _buffer.Size;

		private void Initialize()
		{
			var consoleSize = BufferSize;

			Console.Initialize();
			_buffer.Clear();

			_freezeLock.Freeze();
			ContentContext.SetLimits(consoleSize, consoleSize);
			_freezeLock.Unfreeze();

			Redraw();
		}

		private void Redraw()
		{
			Update(ContentContext.Size.AsRect());
		}

		private void Update(Rect rect)
		{
			Console.OnRefresh();

			rect = Rect.Intersect(rect, Rect.OfSize(BufferSize));
			rect = Rect.Intersect(rect, Rect.OfSize(WindowSize));

			for (int y = rect.Top; y <= rect.Bottom; y++)
			{
				for (int x = rect.Left; x <= rect.Right; x++)
				{
					var position = new Position(x, y);

					var cell = ContentContext[position];

					if (!_buffer.Update(position, cell)) continue;

					try
					{
						Console.Write(position, cell.Character);
					}
					catch (SafeConsoleException)
					{
						rect = Rect.Intersect(rect, Rect.OfSize(WindowSize));
					}
				}
			}
		}

		public void Setup()
		{
			Resize(WindowSize);
		}

		public void Resize(in Size size)
		{
			Console.Size = size;
			_buffer.Initialize(size);

			Initialize();
		}

		public void AdjustBufferSize()
		{
			if (WindowSize != BufferSize)
				Resize(WindowSize);
		}

		public void AdjustWindowSize()
		{
			if (WindowSize != BufferSize)
				Resize(BufferSize);
		}

		public void ReadInput(IReadOnlyCollection<IInputListener> controls)
		{
			while (Console.KeyAvailable)
			{
				var key = Console.ReadKey();
				var inputEvent = new InputEvent(key);

				foreach (var control in controls)
				{
					control?.OnInput(inputEvent);
					if (inputEvent.Handled) break;
				}
			}
		}

		private void BindContent()
		{
			ContentContext = new DrawingContext(new ConsoleManagerDrawingContextListener(this), Content);
		}

		private void UpdateMouseContext()
		{
			MouseContext = MousePosition.HasValue
				? _buffer.GetMouseContext(MousePosition.Value)
				: null;
		}
	}
}
