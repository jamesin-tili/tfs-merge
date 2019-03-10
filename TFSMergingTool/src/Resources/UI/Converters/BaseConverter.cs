using System;
using System.Windows.Markup;

namespace TFSMergingTool.Resources.UI.Converters
{
    /// <summary>
    /// Can be used to instantiate ValueConverter directly in xaml, without declaring it as a resource.
    /// </summary>
    /// <example>Text={Binding Time, Converter={x:MyConverter}},</example>
    public abstract class BaseConverter : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
