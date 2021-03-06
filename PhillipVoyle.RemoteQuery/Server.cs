using PhillipVoyle.RemoteQuery.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PhillipVoyle.RemoteQuery
{
    public interface IQueryableDeserialiser<T>
    {
        Expression DeserialiseCountQuery(CountQuery cq, IQueryable<T> root);
        Expression DeserialiseSortFilterPageQuery(SortFilterPageQuery sfpq, IQueryable<T> root);
    }

    public class Scope
    {
        Dictionary<string, ParameterExpression> parameters;
        Scope parentScope = null;

        public Scope(Scope p)
        {
            parentScope = p;
            parameters = new Dictionary<string, ParameterExpression>();
        }

        bool TryGetValue(string s, out ParameterExpression parameterExpression)
        {
            if (parameters.TryGetValue(s, out parameterExpression))
            {
                return true;
            }
            
            if (parentScope != null && parentScope.TryGetValue(s, out parameterExpression))
            {
                return true;
            }
            return false;
        }
        public ParameterExpression GetParameterExpression(Type t, string s)
        {
            if (TryGetValue(s, out ParameterExpression parameterExpression))
            {
                return parameterExpression;
            }

            var result = Expression.Parameter(t, s);
            parameters[s] = result;
            return result;
        }
    };

    public class GenericScope
    {
        Dictionary<Type, Type> unifiedTypes;
        GenericScope parentScope = null;

        public GenericScope(GenericScope p)
        {
            parentScope = p;
            unifiedTypes = new Dictionary<Type, Type>();
        }
        public bool ContainsKey(Type genericType)
        {
            if (unifiedTypes.ContainsKey(genericType))
            {
                return true;
            }
            if (parentScope != null)
            {
                return parentScope.ContainsKey(genericType);
            }
            return false;
        }

        public Type GetValue(Type genericType)
        {
            if (TryGetValue(genericType, out Type specificType))
            {
                return specificType;
            }
            throw new KeyNotFoundException();
        }
        public bool TryGetValue(Type genericType, out Type specificType)
        {
            if (unifiedTypes.TryGetValue(genericType, out  specificType))
            {
                return true;
            }

            if (parentScope != null && parentScope.TryGetValue(genericType, out specificType))
            {
                return true;
            }
            return false;
        }
        public bool TryUnifyType(Type genericType, Type specificType)
        {
            if (TryGetValue(genericType, out Type storedType))
            {
                return specificType == storedType;
            }

            unifiedTypes[genericType] = specificType;
            return true;
        }
    };

    public class QueryableDeserialiser<T> : IQueryableDeserialiser<T>
    {
        Scope currentScope = null;

        public T WithScope<T>(Func<T> inner)
        {
            Scope oldScope = currentScope;
            currentScope = new Scope(oldScope);
            try
            {
                return inner();
            }
            finally
            {
                currentScope = oldScope;
            }
        }
        public Type GetTypeByName(string name)
        {
            var type = Type.GetType(name);
            if (type == null && name == typeof(T).Name)
            {
                type = typeof(T);
            }
            return type;
        }
        public ExpressionType ToExpressionType(SerialisableExpressionType t)
        {
            return (ExpressionType)(int)t;
        }

        public ParameterExpression BuildParameterExpression(SerialisableExpression expr)
        {

            var type = GetTypeByName(expr.TypeName);
            if (currentScope == null)
            {
                return Expression.Parameter(type, expr.Name);
            }
            else
            {
                return currentScope.GetParameterExpression(type, expr.Name);
            }
        }
        public ElementInit BuildElementInit(SerialisableExpression expr)
        {
            throw new NotImplementedException();
        }
        public NewExpression BuildNewExpression(SerialisableExpression expr)
        {
            throw new NotImplementedException();
        }
        public MemberBinding BuildMemberBind(SerialisableExpression expr)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<MethodInfo> UnifyGenericParameters(GenericScope scope, Type genericType, int genericParameter, Type[] specificArgs, Func<GenericScope, IEnumerable<MethodInfo>> continuation)
        {
            if (genericParameter == genericType.GetGenericArguments().Length)
            {
                return continuation(scope);
            }
            else
            {
                var gT = genericType.GetGenericArguments()[genericParameter];
                var aT = specificArgs[genericParameter];

                return UnifyTypeAndBaseClasses(scope, gT, aT,
                    scope => UnifyGenericParameters(scope, genericType, genericParameter + 1, specificArgs, continuation));
            }
        }

        public IEnumerable<MethodInfo> UnifyType(GenericScope scope, Type tGeneric, Type tSpecific, Func<GenericScope, IEnumerable<MethodInfo>> continuation)
        {
            if (tGeneric.IsGenericMethodParameter)
            {
                if (scope.TryGetValue(tGeneric, out Type unifiedType))
                {
                    if (unifiedType.IsAssignableFrom(tSpecific))
                    {

                        return continuation(scope);
                    }
                    else
                    {
                        return Enumerable.Empty<MethodInfo>();
                    }
                }
                else
                {
                    if (scope.TryUnifyType(tGeneric, tSpecific))
                    {
                        return continuation(scope);
                    }
                    else
                    {
                        return Enumerable.Empty<MethodInfo>();
                    }
                }
            }
            else if (tGeneric.IsAssignableFrom(tSpecific))
            {
                return continuation(scope);
            }
            else if (tGeneric.ContainsGenericParameters)
            {
                if (tGeneric.Name != tSpecific.Name)
                    return Enumerable.Empty<MethodInfo>();

                if (tGeneric.GetGenericArguments().Length != tSpecific.GetGenericArguments().Length)
                    return Enumerable.Empty<MethodInfo>();

                return UnifyGenericParameters(scope, tGeneric, 0, tSpecific.GetGenericArguments(), continuation);
            }

            return Enumerable.Empty<MethodInfo>();
        }
        public IEnumerable<MethodInfo> UnifyTypeAndBaseClasses(GenericScope scope, Type tGeneric, Type tSpec, Func<GenericScope, IEnumerable<MethodInfo>> continuation)
        {
            var typesToCheck = new Type[] { tSpec }.Union(tSpec.GetInterfaces()).Distinct();

            return typesToCheck.SelectMany(tSpecific =>
            {
                GenericScope newScope = new GenericScope(scope);
                return UnifyType(newScope, tGeneric, tSpecific, continuation);
            });
        }
        public IEnumerable<MethodInfo> UnifyMethodParameters(GenericScope scope, int nParameter, MethodInfo method, Type[] argTypes)
        {
            if (nParameter == argTypes.Length)
            {
                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length == 0)
                {
                    return new MethodInfo[] { method };
                }
                else
                {
                    foreach (var genericArgument in genericArguments)
                    {
                        if (!scope.ContainsKey(genericArgument))
                        {
                            return Enumerable.Empty<MethodInfo>();
                        }
                    }

                    var genericMethod = method.MakeGenericMethod(genericArguments.Select(arg => scope.GetValue(arg)).ToArray());
                    return new MethodInfo[] { genericMethod };
                }
            }
            else
            {
                var parameter = method.GetParameters()[nParameter];
                var parameterType = parameter.ParameterType;
                var argumentType = argTypes[nParameter];
                return UnifyTypeAndBaseClasses(scope, parameterType, argumentType, scope => UnifyMethodParameters(scope, nParameter + 1, method, argTypes));
            }
            
        }
        public IEnumerable<MethodInfo> CheckParameters(MethodInfo method, Type[] argumentTypes)
        {
            var methodParameters = method.GetParameters();

            if (methodParameters.Length != argumentTypes.Length)
                return Enumerable.Empty<MethodInfo>();

            GenericScope genericScope = new GenericScope(null);
            return UnifyMethodParameters(genericScope, 0, method, argumentTypes);
        }
        
        public MethodInfo[] FindExtensionMethods(Type[] parameterTypes, string methodName)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes());

            var methods = types.Where(type => type.CustomAttributes.Any(x => x.AttributeType == typeof(ExtensionAttribute)))
                .Where(type => type.IsSealed && !type.IsGenericType && !type.IsNested)
                .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(x => x.Name == methodName))
                .Select(type =>
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(method => method.CustomAttributes.Any(x => x.AttributeType == typeof(ExtensionAttribute)))
                        .Where(x => x.Name == methodName).ToArray();
                    return methods;
                }).SelectMany(x => x)
                .ToArray();

            var filteredMethods = methods.SelectMany(x => CheckParameters(x, parameterTypes)).ToArray();

            return filteredMethods;
        }

        public MethodInfo FindExtensionMethod(Type[] ts, string methodName)
        {
            var extensionMethods = FindExtensionMethods(ts, methodName);
            var extensionMethod = extensionMethods.First();
            return extensionMethod;
        }

        public Expression BuildExpression(SerialisableExpression expr)
        {
            if (expr == null)
            {
                return null;
            }
            var type = ToExpressionType(expr.ExpressionType);
            switch (type)
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
                        return Expression.MakeBinary(
                            type,
                            BuildExpression(expr.Expressions[0]),
                            BuildExpression(expr.Expressions[1]));
                    }
                case ExpressionType.ArrayLength:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.Not:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    {
                        var operand = BuildExpression(expr.Expressions[0]);

                        return Expression.MakeUnary(
                            type,
                            BuildExpression(expr.Expressions[0]),
                            null //type
                            );
                    }
                case ExpressionType.Call:
                    {
                        var expressions = expr.Expressions.Select(BuildExpression).ToArray();
                        var method = (MethodInfo)null;
                        if (expressions[0] == null)
                        {
                            var parameterType = expressions[1].Type;
                            method = FindExtensionMethod(expressions.Skip(1).Select(x => x.Type).ToArray(), expr.Name); //todo: parameter check

                            return Expression.Call(expressions[0], method, expressions.Skip(1).ToArray());
                        }
                        else
                        {
                            var methods = expressions[0].Type.GetMethods().Where(x => x.Name == expr.Name).ToArray();
                            method = methods.FirstOrDefault(); //TODO: parameter check

                            var arguments = expressions.Skip(1);

                            method = methods.SelectMany(x => CheckParameters(x, arguments.Select(arg => arg.Type).ToArray())).Single();

                            return Expression.Call(expressions[0], method, expressions.Skip(1).ToArray());
                        }

                    }
                case ExpressionType.Conditional:
                    {
                        return Expression.Condition(
                            BuildExpression(expr.Expressions[0]),
                            BuildExpression(expr.Expressions[1]),
                            BuildExpression(expr.Expressions[2]));
                    }
                case ExpressionType.Constant:
                    {
                        return Expression.Constant(expr.Value);
                    }
                case ExpressionType.Invoke:
                    {
                        return Expression.Invoke(
                            BuildExpression(expr.Expressions[0]),
                            expr.Expressions.Skip(1).Select(BuildExpression));
                    }
                case ExpressionType.Lambda:
                    {
                        return WithScope(() =>
                        {
                            return Expression.Lambda(
                                BuildExpression(expr.Expressions.Last()),
                                expr.Expressions.Take(expr.Expressions.Length - 1)
                                    .Select(BuildParameterExpression)
                                );
                        });
                    }
                case ExpressionType.ListInit:
                    {
                        return Expression.ListInit(
                            BuildNewExpression(expr.Expressions[0]),
                            expr.Expressions.Skip(1).Select(BuildElementInit));
                    }
                case ExpressionType.MemberAccess:
                    {
                        var expression = BuildExpression(expr.Expressions[0]);
                        var member = expression.Type.GetMember((string)expr.Name).Single();
                        return Expression.MakeMemberAccess(
                            expression,
                            member);
                    }
                case ExpressionType.MemberInit:
                    {
                        var expression = BuildNewExpression(expr.Expressions[0]);
                        return Expression.MemberInit(
                            expression,
                            expr.Expressions.Skip(1).Select(BuildMemberBind));
                    }
                case ExpressionType.New:
                    {
                        var newType = Type.GetType((string)expr.TypeName);
                        var constructor = (ConstructorInfo)null;
                        var memberInfos = expr.Expressions[0].Expressions.Select(x => newType.GetMember((string)x.Name).Single());

                        return Expression.New(
                            constructor,
                            expr.Expressions.Skip(1).Select(BuildExpression),
                            memberInfos
                        );
                    }
                case ExpressionType.NewArrayInit:
                    {
                        var newType = Type.GetType((string)expr.TypeName);
                        return Expression.NewArrayInit(newType,
                            expr.Expressions.Select(BuildExpression));
                    }
                case ExpressionType.NewArrayBounds:
                    {
                        var newType = Type.GetType((string)expr.TypeName);
                        return Expression.NewArrayBounds(
                            newType,
                            expr.Expressions.Select(BuildExpression));
                    }
                case ExpressionType.Parameter:
                    {
                        return BuildParameterExpression(expr);
                    }
                case ExpressionType.TypeIs:
                    {
                        var typeIs = Type.GetType(expr.TypeName);
                        return Expression.TypeIs(BuildExpression(expr.Expressions[0]), typeIs);
                    }
                default:
                    {
                        throw new Exception($"Unsupported Expression Type: {type}");
                    }
            }
            throw new NotImplementedException();
        }
        public Expression BuildFilterExpression(SerialisableExpression ex, Expression root)
        {
            var whereParameter = BuildExpression(ex);
            var whereMethod = typeof(Queryable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(mi => mi.Name == "Where")
                .SelectMany(method => CheckParameters(method, new Type[] { root.Type, whereParameter.Type }))
                .First();

            return Expression.Call((Expression)null, whereMethod, root, whereParameter);
        }
        public Expression DeserialiseCountQuery(CountQuery cq, IQueryable<T> root)
        {
            var expr = (Expression)Expression.Constant(root);
            if (cq.FilterBy != null)
            {
                expr = BuildFilterExpression(cq.FilterBy, expr);
            }

            var countMethod = typeof(Queryable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(mi => mi.Name == "Count")
                .SelectMany(method => CheckParameters(method, new Type[] { expr.Type }))
                .First();

            return Expression.Call((Expression)null, countMethod, expr);
        }

        public Expression BuildOrderByExpression(SortExpression sortExpression, Expression expr)
        {
            string name = "OrderBy";

            if (sortExpression.SortOrder == OrderByDirection.Descending)
            {
                name = "OrderByDescending";
            }

            var orderByParameter = BuildExpression(sortExpression.SortSelector);

            var orderByMethods = typeof(Queryable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(mi => mi.Name == name)
                .SelectMany(method => CheckParameters(method, new Type[] { expr.Type, orderByParameter.Type }))
                .ToArray();


            var orderByMethod = orderByMethods.First();
            return Expression.Call((Expression)null, orderByMethod, expr, orderByParameter);
        }

        public Expression DeserialiseSortFilterPageQuery(SortFilterPageQuery sfpq, IQueryable<T> root)
        {
            Expression expr = Expression.Constant(root);
            if (sfpq.FilterBy != null)
            {
                expr = BuildFilterExpression(sfpq.FilterBy, expr);
            }
            if (sfpq.OrderBy != null)
            {
                expr = BuildOrderByExpression(sfpq.OrderBy, expr);
            }
            if (sfpq.SkipCount != null)
            {
                var skipMethod = typeof(Queryable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(mi => mi.Name == "Skip" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T));
                expr = Expression.Call((Expression)null, skipMethod, expr, Expression.Constant(sfpq.SkipCount));
            }
            if (sfpq.TakeCount != null)
            {
                var takeMethod = typeof(Queryable).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(mi => mi.Name == "Take" && mi.IsGenericMethodDefinition && mi.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T));
                expr = Expression.Call((Expression)null, takeMethod, expr, Expression.Constant(sfpq.TakeCount));
            }

            return expr;
        }
    };

    public class QueryableExecutor<T> : IQueryEndpoint<T>
    {
        public QueryableExecutor(IQueryable<T> queryable)
        {
            Queryable = queryable;
            QueryableDeserialiser = new QueryableDeserialiser<T>();
        }

        public IQueryable<T> Queryable { get; set; }
        public IQueryableDeserialiser<T> QueryableDeserialiser { get; set; }
        public int ExecuteCountQuery(CountQuery cq)
        {
            return Queryable.Provider.Execute<int>(QueryableDeserialiser.DeserialiseCountQuery(cq, Queryable));
        }

        public IEnumerable<T> ExecuteSortFilterPageQuery(SortFilterPageQuery sfpq)
        {
            return Queryable.Provider.CreateQuery<T>(QueryableDeserialiser.DeserialiseSortFilterPageQuery(sfpq, Queryable));
        }
    }
}
