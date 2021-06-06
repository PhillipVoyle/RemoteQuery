using System;
using Xunit;
using PhillipVoyle.RemoteQuery;
using PhillipVoyle.RemoteQuery.DataTypes;
using System.Collections.Generic;
using System.Linq;

namespace PhillipVoyle.RemoteQuery.Tests
{
    public class TestData
    {
        public string TestProperty1 { get; set; }
        public int TestProperty2 { get; set; }

        public IEnumerable<int> TestProperty3 { get; set; }
    };

    public class UnitTest1
    {
        TestData [] TestData { get; set; }
        public UnitTest1()
        {
            TestData = new TestData[] {
                new TestData
                {
                    TestProperty1 = "Test1",
                    TestProperty2 = 12,
                    TestProperty3 = new int[] {35, 66, 2567}
                },
                new TestData
                {
                    TestProperty1 = "Test2",
                    TestProperty2 = 76789,
                    TestProperty3 = new int[] {35, 18, 19}
                },
                new TestData
                {
                    TestProperty1 = "Coronary",
                    TestProperty2 = 8921,
                    TestProperty3 = new int[] {22}
                }
            };
        }

        IQueryable<TestData> GetNewQuery()
        {
            return new QueryableProvider<TestData>(new QueryableExecutor<TestData>(TestData.AsQueryable())).NewQuery();
        }

        [Fact]
        public void SimpleTestCountMatches()
        {

            var testDataQuery = GetNewQuery();
            var query1 = testDataQuery.Where(x => x.TestProperty3.Contains(19));
            var count = query1.Count();

            var array = query1.ToArray();
            Assert.Equal(array.Count(), count);
        }

        [Fact]
        public void FilterBySubstring()
        {
            var testDataQuery = GetNewQuery();
            var data = testDataQuery.Where(t => t.TestProperty1.ToLower().Contains("est")).ToArray();

            Assert.True(data.Length == 2);
        }


        [Fact]
        public void OrderByInteger()
        {
            var testDataQuery = GetNewQuery();
            var data = testDataQuery.OrderBy(t => t.TestProperty2).ToArray() //select not implemented
                .Select(t => t.TestProperty2).ToArray();

            Assert.True(data.Length == 3);
            Assert.Equal(data, new int[]
            {
                12,
                8921,
                76789
            });
        }
        [Fact]
        public void OrderByIntegerDesc()
        {
            var testDataQuery = GetNewQuery();
            var data = testDataQuery.OrderByDescending(t => t.TestProperty2).ToArray() //select not implemented
                .Select(t => t.TestProperty2).ToArray();

            Assert.True(data.Length == 3);
            Assert.Equal(data, new int[]
            {
                76789,
                8921,
                12
            });
        }
    }
}
