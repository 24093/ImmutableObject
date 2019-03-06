using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

namespace DataObjects
{
    internal class Program
    {
        private static void Main()
        {
            try
            {
                //// Create a customer object.
                var p1 = new[] { new Purchase(3342), new Purchase(5648), new Purchase(7577) };
                var customer1 = new Customer("Oleg", 1111, p1);

                WriteJson(customer1);


                //// Change a property by creating a new object. 
                ////customer1.CustomerNumber = 555;
                ////customer1 = new Customer(customer1.Name, 555, customer1.Purchases);


                //// Use With pattern.
                customer1 = customer1
                    .WithCustomerNumber(555);

                WriteJson(customer1);


                //// Modify a immutable list.
                var customer2 = customer1
                    .WithPurchases(customer1.Purchases.Add(new Purchase(9000)));

                WriteJson(customer2);


                //// Replace an item inside a immutable list.
                var p3 = customer2.Purchases[3];
                var customer3 = customer2
                    .WithPurchases(customer2.Purchases.Replace(p3, p3.WithPurchaseId(9999)));

                WriteJson(customer3);


                // Violate requirement at object creation.
                var customer4 = new Customer(null, -2, null);
            }
            catch (ObjectRequirementException ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                Console.ReadKey();
            }
        }

        private static void WriteJson(object obj)
        {
            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
            Console.WriteLine();
        }
    }
    
    internal sealed class Customer : ImmutableObject<Customer>
    {
        public string Name { get; private set; }

        public int CustomerNumber { get; private set; }

        public ImmutableList<Purchase> Purchases { get; private set; }

        public Customer(string name, int customerNumber, IEnumerable<Purchase> purchases)
        {
            Require.Throw(
                Require.NotNull(() => name, () => purchases),
                Require.NotEmpty(() => name),
                Require.Positive(() => customerNumber));

            Name = name;
            CustomerNumber = customerNumber;
            Purchases = purchases.ToImmutableList();
        }

        public override Customer DeepClone()
        {
            return new Customer(Name, CustomerNumber, Purchases);
        }

        public Customer WithName(string name) => With(x => x.Name = name);

        public Customer WithCustomerNumber(int customerNumber) => With(x => x.CustomerNumber = customerNumber);

        public Customer WithPurchases(IEnumerable<Purchase> purchases) => With(x => x.Purchases = purchases.ToImmutableList());
    }

    internal sealed class Purchase : ImmutableObject<Purchase>
    {
        public int PurchaseId { get; private set; }

        public Purchase(int purchaseId)
        {
            Require.Positive(() => purchaseId);

            PurchaseId = purchaseId;
        }

        public override Purchase DeepClone()
        {
            return new Purchase(PurchaseId);
        }

        public Purchase WithPurchaseId(int purchaseId) => With(x => x.PurchaseId = purchaseId);
    }

    internal abstract class ImmutableObject<T> : IDeepCloneable<T>
    {
        public abstract T DeepClone();

        protected T With(Action<T> modifier)
        {
            var clone = DeepClone();
            modifier.Invoke(clone);
            return clone;
        }
    }

    internal interface IDeepCloneable<T>
    {
        T DeepClone();
    }
    
    internal static class Require
    {
        public static IEnumerable<ObjectRequirementException> NotNull(params Expression<Func<object>>[] values)
        {
            return Try(x => x != null, RequirementType.ValueMustNotBeNull, values);
        }

        public static IEnumerable<ObjectRequirementException> Positive(params Expression<Func<int>>[] values)
        {
            return Try(x => x > 0, RequirementType.ValueHasToBePositive, values);
        }

        public static IEnumerable<ObjectRequirementException> NotEmpty(params Expression<Func<string>>[] values)
        {
            return Try(x => !string.IsNullOrEmpty(x), RequirementType.ValueMustNotBeEmpty, values);
        }

        public static void Throw(params IEnumerable<ObjectRequirementException>[] exceptions)
        {
            var aggregateException = new ObjectRequirementException();

            foreach (var exceptionList in exceptions)
            {
                foreach (var exception in exceptionList)
                {
                    aggregateException.Add(exception);
                }
            }

            if (aggregateException.Errors.Any())
            {
                throw aggregateException;
            } 
        }
        
        private static IEnumerable<ObjectRequirementException> Try<T>(Predicate<T> condition, RequirementType requirementType, params Expression<Func<T>>[] values)
        {
            foreach (var expression in values)
            {
                var value = GetValue(expression);

                if (!condition(value))
                {
                    var name = GetName(expression);
                    yield return new ObjectRequirementException(name, requirementType);
                }
            }
        }
        
        private static T GetValue<T>(Expression<Func<T>> expression)
        {
            return expression.Compile().Invoke();
        }

        private static string GetName<T>(Expression<Func<T>> expression)
        {
            var memberExpression = (expression.Body as MemberExpression) ??
                                   (expression.Body as UnaryExpression)?.Operand as MemberExpression;

            return memberExpression?.Member.Name;
        }
    }

    internal enum RequirementType
    {
        None,
        ValueMustNotBeNull,
        ValueHasToBePositive,
        ValueMustNotBeEmpty
    };
    
    internal class ObjectRequirementException : Exception
    {
        public Dictionary<string, ISet<RequirementType>> Errors;

        public ObjectRequirementException()
        {
            Errors = new Dictionary<string, ISet<RequirementType>>();
        }

        public ObjectRequirementException(string propertyName, RequirementType requirementType)
            : this()
        {
            Add(propertyName, requirementType);
        }

        public void Add(string propertyName, RequirementType requirementType)
        {
            if (!Errors.ContainsKey(propertyName))
            {
                Errors.Add(propertyName, new HashSet<RequirementType>());
            }

            Errors[propertyName].Add(requirementType);
        }

        public void Add(ObjectRequirementException exception)
        {
            foreach (var error in exception.Errors)
            {
                foreach (var type in error.Value)
                {
                    Add(error.Key, type);
                }
            }
        }

        public override string ToString()
        {
            var text = string.Empty;

            foreach (var error in Errors)
            {
                text += error.Key + ": " + string.Join(' ', error.Value) + Environment.NewLine;
            }

            return text;
        }
    }
}
