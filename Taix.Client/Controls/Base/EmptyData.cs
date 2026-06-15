using System;
using Avalonia.Controls.Primitives;

namespace Taix.Client.Controls.Base;

public class EmptyData : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(EmptyData);
}