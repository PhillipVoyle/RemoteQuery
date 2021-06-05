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

    public class UnitTest1 : IQueryEndpoint<TestData>
    {

        public int ExecuteCountQuery(CountQuery cq)
        {
            return 0;
        }

        public IEnumerable<TestData> ExecuteSortFilterPageQuery(SortFilterPageQuery sfpq)
        {
            return new TestData[] { };
        }

        [Fact]
        public void Test1()
        {
            var testDataQuery = new QueryableProvider<TestData>(this).NewQuery();
            var query1 = testDataQuery.Where(x => x.TestProperty3.Contains(19));
            var array = query1.ToArray();
            var count = testDataQuery.Count();
            Assert.Equal(array.Count(), count);
        }
    }
}
