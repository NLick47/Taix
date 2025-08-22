using System;
using Avalonia.Controls.Primitives;

namespace UI.Controls.Base;

public class EmptyData : TemplatedControl
{
    protected override Type StyleKeyOverride => typeof(EmptyData);
}