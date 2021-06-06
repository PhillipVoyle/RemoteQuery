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
                }
            };
        }

        [Fact]
        public void Test1()
        {
            var testDataQuery = new QueryableProvider<TestData>(new QueryableExector<TestData>(TestData.AsQueryable())).NewQuery();
            var query1 = testDataQuery.Where(x => x.TestProperty3.Contains(19));
            var count = query1.Count();

            var array = query1.ToArray();
            Assert.Equal(array.Count(), count);
        }
    }
}
