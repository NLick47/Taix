using System;
using System.ComponentModel;
using System.Linq.Expressions;
using Taix.Client.Foundation.Rx;

namespace Taix.Client.Foundation;

public static class ObservablePropertyChangedExtensions
{
    public static IObservable<TProperty> WhenPropertyChanged<TSource, TProperty>(
        this TSource source,
        Expression<Func<TSource, TProperty>> propertyExpression)
        where TSource : INotifyPropertyChanged
    {
        var memberName = GetMemberName(propertyExpression);
        var getter = propertyExpression.Compile();

        return RxObservable.Create<TProperty>(observer =>
        {
            PropertyChangedEventHandler handler = (_, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == memberName)
                {
                    observer.OnNext(getter(source));
                }
            };

            source.PropertyChanged += handler;
            observer.OnNext(getter(source));
            return Disposable.Create(() => source.PropertyChanged -= handler);
        });
    }

    private static string GetMemberName<TSource, TProperty>(
        Expression<Func<TSource, TProperty>> expression)
    {
        return expression.Body switch
        {
            MemberExpression me => me.Member.Name,
            UnaryExpression { Operand: MemberExpression me } => me.Member.Name,
            _ => throw new ArgumentException("表达式必须是成员访问表达式。", nameof(expression))
        };
    }
}
