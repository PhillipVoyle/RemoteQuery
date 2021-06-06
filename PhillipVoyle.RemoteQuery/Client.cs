using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using PhillipVoyle.RemoteQuery.DataTypes;

namespace PhillipVoyle.RemoteQuery
{
    public interface IQueryEndpoint<T>
    {
        IEnumerable<T> ExecuteSortFilterPageQuery(SortFilterPageQuery sfpq);
        int ExecuteCountQuery(CountQuery cq);
    };

    public class Queryable<T> : IOrderedQueryable<T>
    {
        public Queryable()
        {
            ElementType = typeof(T);
            Expression = Expression.Constant(this);
        }

        public Queryable(Expression expr, Type type, QueryableProvider<T> queryableProvider)
        {
            ElementType = type;
            Expression = expr;
            QueryProvider = queryableProvider;
        }
        public Type ElementType { get; set; }

        public Expression Expression { get; set; }

        public QueryableProvider<T> QueryProvider { get; set; }
        public IQueryProvider Provider => QueryProvider;
        internal IEnumerator<T> internalGetEnumerator()
        {
            return QueryProvider.GetEnumerable(Expression).GetEnumerator();
        }
        public IEnumerator<T> GetEnumerator()
        {
            return internalGetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return internalGetEnumerator();
        }
    }

    public interface IQuerySerialiser
    {
        SortFilterPageQuery SerialiseSortFilterPageQuery(Expression ex, out object queryable);
        CountQuery SerialiseCountQuery(Expression ex, out object queryable);
    }

    public class QuerySerialiser : IQuerySerialiser
    {
        public static SerialisableExpressionType ToSerialisableExpressionType(ExpressionType et)
        {
            return (SerialisableExpressionType)(int)et; //todo: switch
        }
        public SortFilterPageQuery SerialiseSortFilterPageQuery(Expression expr, out object queryable)
        {
            return BuildSortFilterPageQuery(new SortFilterPageQuery(), expr, out queryable);
        }
        public SortFilterPageQuery BuildSortFilterPageQuery(SortFilterPageQuery sfpq, Expression expr, out object queryable)
        {
            if (expr.NodeType == ExpressionType.Call)
            {
                var call = (MethodCallExpression)expr;
                if (call.Method.Name == "Where")
                {
                    sfpq.FilterBy = ToSerialisableExpression(call.Arguments[1]);
                    return BuildSortFilterPageQuery(sfpq, call.Arguments[0], out queryable);
                }
                else if (call.Method.Name == "OrderBy")
                {
                    sfpq.OrderBy = new SortExpression
                    {
                        SortOrder = OrderByDirection.Ascending,
                        SortSelector = ToSerialisableExpression(call.Arguments[1])
                    };
                    return BuildSortFilterPageQuery(sfpq, call.Arguments[0], out queryable);
                }
                else if (call.Method.Name == "OrderByDescending")
                {
                    sfpq.OrderBy = new SortExpression
                    {
                        SortOrder = OrderByDirection.Descending,
                        SortSelector = ToSerialisableExpression(call.Arguments[1])
                    };
                    return BuildSortFilterPageQuery(sfpq, call.Arguments[0], out queryable);
                }
                else if (call.Method.Name == "Skip")
                {
                    sfpq.SkipCount = (int)((ConstantExpression)call.Arguments[1]).Value;
                    return BuildSortFilterPageQuery(sfpq, call.Arguments[0], out queryable);
                }
                else if (call.Method.Name == "Take")
                {
                    sfpq.TakeCount = (int)((ConstantExpression)call.Arguments[1]).Value;
                    return BuildSortFilterPageQuery(sfpq, call.Arguments[0], out queryable);
                }
            }
            else if (expr.NodeType == ExpressionType.Constant)
            {
                queryable = ((ConstantExpression)expr).Value;
                return sfpq;
            }
            throw new NotImplementedException();
        }

        public SerialisableExpression ToSerialisableExpression(Expression expr)
        {
            if (expr == null)
            {
                return null;
            }
            var type = ToSerialisableExpressionType(expr.NodeType);
            switch (expr.NodeType)
            {
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.ArrayIndex:
                case ExpressionType.Coalesce:
                case ExpressionType.Divide:
                case ExpressionType.Equal:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LeftShift:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.Modulo:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.NotEqual:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.Power:
                case ExpressionType.RightShift:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    {
                        var binary = (BinaryExpression) expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[]
                            {
                                ToSerialisableExpression(binary.Left),
                                ToSerialisableExpression(binary.Right)
                            }
                        };
                    }
                case ExpressionType.ArrayLength:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.Not:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    {
                        var unary = (UnaryExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[]
                            {
                                ToSerialisableExpression(unary.Operand)
                            }
                        };
                    }
                case ExpressionType.Call:
                    {
                        var methodCall = (MethodCallExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            Name = methodCall.Method.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[]
                            {
                                ToSerialisableExpression(methodCall.Object)
                            }.Union(methodCall.Arguments.Select(ToSerialisableExpression))
                            .ToArray()
                        };
                    }
                case ExpressionType.Conditional:
                    {
                        var conditional = (ConditionalExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[]
                            {
                                ToSerialisableExpression(conditional.Test),
                                ToSerialisableExpression(conditional.IfTrue),
                                ToSerialisableExpression(conditional.IfFalse)
                            }
                        };
                    }
                case ExpressionType.Constant:
                    {
                        var constant = (ConstantExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Value = constant.Value
                        };
                    }
                case ExpressionType.Invoke:
                    {
                        var invoke = (InvocationExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new Expression[] { invoke.Expression }
                                .Union(invoke.Arguments)
                                .Select(ToSerialisableExpression).ToArray()
                        };
                    }
                case ExpressionType.Lambda:
                    {
                        var lambda = (LambdaExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = lambda.Parameters
                                .Union(new Expression[] {lambda.Body})
                                .Select(ToSerialisableExpression).ToArray()
                        };
                    }
                case ExpressionType.ListInit:
                    {
                        var listInit = (ListInitExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions =
                                new SerialisableExpression[]
                                {
                                    ToSerialisableExpression(listInit.NewExpression)
                                }.Union(listInit.Initializers.Select(init =>
                                    new SerialisableExpression
                                    {
                                        TypeName = expr.Type.Name,
                                        Name = init.AddMethod?.Name,
                                        ExpressionType = SerialisableExpressionType.Call,
                                        Expressions = init.Arguments.Select(ToSerialisableExpression).ToArray()
                                    })).ToArray(),
                            Name = listInit.Type.Name
                        };
                    }
                case ExpressionType.MemberAccess:
                    {
                        var memberAccess = (MemberExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression []
                            {
                               ToSerialisableExpression(memberAccess.Expression)
                            },
                            Name = memberAccess.Member.Name
                        };
                    }
                case ExpressionType.MemberInit:
                    {
                        var memberInit = (MemberInitExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[]
                            {
                                ToSerialisableExpression(memberInit.NewExpression)
                            }.Union(memberInit.Bindings.Select(binding => new SerialisableExpression
                            {
                                TypeName = binding.Member.DeclaringType.Name,
                                ExpressionType = SerialisableExpressionType.MemberInit,
                                Name = binding.Member.Name
                            })).ToArray()
                        };
                    }
                case ExpressionType.New:
                    {
                        var newExpression = (NewExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[] {
                                new SerialisableExpression
                                {
                                    Name = "Members",
                                    ExpressionType = SerialisableExpressionType.New,
                                    Expressions = newExpression.Members.Select(x =>
                                        new SerialisableExpression{
                                            TypeName = x.DeclaringType.Name,
                                            ExpressionType = SerialisableExpressionType.MemberInit,
                                            Name = x.Name
                                        }).ToArray()
                                }
                            }.Union(newExpression.Arguments.Select(ToSerialisableExpression))
                            .ToArray(),
                        };
                    }
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    {
                        var newArrayInit = (NewArrayExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = newArrayInit.Expressions.Select(ToSerialisableExpression).ToArray(),
                        };
                    }
                case ExpressionType.Parameter:
                    {
                        var parameter = (ParameterExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Name = parameter.Name
                        };
                    }
                case ExpressionType.TypeIs:
                    {
                        var typeIs = (TypeBinaryExpression)expr;
                        return new SerialisableExpression
                        {
                            TypeName = expr.Type.Name,
                            ExpressionType = type,
                            Expressions = new SerialisableExpression[] { ToSerialisableExpression(typeIs.Expression) },
                        };
                    }
                default:
                    {
                        throw new Exception($"Unsupported Expression Type: {expr.NodeType}");
                    }
            }
            throw new NotImplementedException();
        }

        public CountQuery BuildCountQuery(CountQuery cq, Expression ex, out object queryable)
        {
            if (ex.NodeType == ExpressionType.Call)
            {
                var call = (MethodCallExpression)ex;
                if (call.Method.Name == "Where")
                {
                    cq.FilterBy = ToSerialisableExpression(call.Arguments[1]);
                    return BuildCountQuery(cq, call.Arguments[0], out queryable);
                }
            }
            else if (ex.NodeType == ExpressionType.Constant)
            {
                queryable = ((ConstantExpression)ex).Value;
                return cq;
            }
            throw new NotImplementedException();
        }

        public CountQuery SerialiseCountQuery(Expression ex, out object queryable)
        {
            if (ex.NodeType == ExpressionType.Call)
            {
                var call = (MethodCallExpression)ex;
                if (call.Method.Name == "Count")
                {
                    return BuildCountQuery(new CountQuery(), call.Arguments[0], out queryable);
                }
            }
            throw new NotImplementedException();
        }
    }

    public class QueryableProvider<T> : IQueryProvider
    {
        public IQuerySerialiser QuerySerialiser { get; set; }
        public IQueryEndpoint<T> QueryEndpoint { get; set; }

        public QueryableProvider()
        {
            QuerySerialiser = new QuerySerialiser();
        }
        public QueryableProvider(IQueryEndpoint<T> endpoint, IQuerySerialiser serialiser = null)
        {
            QueryEndpoint = endpoint;
            QuerySerialiser = serialiser ?? new QuerySerialiser();
        }

        public IEnumerable<T> GetEnumerable(Expression e)
        {
            object queryable;
            var serialisableExpression = QuerySerialiser.SerialiseSortFilterPageQuery(e, out queryable);
            return QueryEndpoint.ExecuteSortFilterPageQuery(serialisableExpression);
        }
        public IQueryable CreateQuery(Expression expression)
        {
            if (expression.Type == typeof(T))
            {
                return new Queryable<T>(
                    expression,
                    expression.Type,
                    this);
            }
            throw new NotImplementedException();
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            if (typeof(TElement) == typeof(T))
            {
                return (IQueryable<TElement>)new Queryable<T>(
                    expression,
                    expression.Type,
                    this);
            }
            throw new NotImplementedException();
        }

        public object Execute(Expression expression)
        {
            if (expression.Type == typeof(int))
            {
                object queryable;
                var serialisableExpression = QuerySerialiser.SerialiseCountQuery(expression, out queryable);
                return QueryEndpoint.ExecuteCountQuery(serialisableExpression);
            }
            throw new NotImplementedException();
        }

        public TResult Execute<TResult>(Expression expression)
        {
            if ((typeof(TResult) == typeof(int)) && (expression.Type == typeof(int)))
            {
                object queryable;
                var serialisableExpression = QuerySerialiser.SerialiseCountQuery(expression, out queryable);
                return (TResult)(object)QueryEndpoint.ExecuteCountQuery(serialisableExpression);
            }
            throw new NotImplementedException();
        }
        public IQueryable<T> NewQuery()
        {
            return new Queryable<T>() { QueryProvider = this };
        }
    }
}
