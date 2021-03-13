﻿using System;
using System.ComponentModel;
using Shiny.Stores;
using Xunit;


namespace Shiny.Tests.Stores
{
    public partial class StoreTests
    {
        [Category("Types")]
        [Theory]
        [MemberData(nameof(Data))]
        public void Type_DateTimeOffset(IKeyValueStore store)
        {
            this.currentStore = store;
            var dt = DateTimeOffset.Now;
            this.currentStore.Set("now", dt);
            var result = this.currentStore.Get<DateTimeOffset>("now");
            Assert.Equal(dt, result);
        }


        //[Theory]
        //[MemberData(nameof(Data))]
        //public virtual void NullableEnums(IKeyValueStore store)
        //{
        //    this.currentStore = store;
        //    var value = this.currentStore.Get<MyTestEnum?>(nameof(NullableEnums));
        //    Assert.Null(value);

        //    value = this.currentStore.Get<MyTestEnum?>(nameof(NullableEnums));
        //    Assert.Equal(MyTestEnum.Bye, value);

        //    this.currentStore.Set(nameof(NullableEnums), MyTestEnum.Hi);
        //    value = this.currentStore.Get<MyTestEnum?>(nameof(NullableEnums));
        //    Assert.Equal(MyTestEnum.Hi, value);

        //    this.currentStore.Set(nameof(NullableEnums), null);
        //    value = this.currentStore.Get<MyTestEnum?>(nameof(NullableEnums));
        //    Assert.Null(value);
        //}




        //[Theory]
        //[MemberData(nameof(Data))]
        //public void DateTimeNullTest(IKeyValueStore store)
        //{
        //    this.currentStore = store;
        //    var dt = new DateTime(1999, 12, 31, 23, 59, 0);
        //    var nvalue = this.currentStore.Get<DateTime?>("DateTimeNullTest");
        //    Assert.True(nvalue == null, "Should be null");

        //    this.currentStore.Set("DateTimeNullTest", dt);
        //    nvalue = this.currentStore.Get<DateTime?>("DateTimeNullTest");
        //    Assert.Equal(nvalue, dt);
        //}




        //[Theory]
        //[MemberData(nameof(Data))]
        //public void LongTest(IKeyValueStore store)
        //{
        //    this.currentStore = store;
        //    long value = 1;
        //    this.currentStore.Set("LongTest", value);
        //    var value2 = this.currentStore.Get<long>("LongTest");
        //    Assert.Equal(value, value2);
        //}


        //[Theory]
        //[MemberData(nameof(Data))]
        //public void GuidTest(IKeyValueStore store)
        //{
        //    this.currentStore = store;
        //    Assert.Equal(this.currentStore.Get<Guid>("GuidTest"), Guid.Empty);

        //    var guid = new Guid();
        //    this.currentStore.Set("GuidTest", guid);
        //    Assert.Equal(this.currentStore.Get<Guid>("GuidTest"), guid);
        //}


        //[Fact]
        //public void NullBools()
        //{
        //    this.currentStore.Set<bool?>("SetNullBool", null);
        //    var value = this.currentStore.Get<bool?>("SetNullBool");
        //    Assert.Null(value);

        //    this.currentStore.Set<bool?>("SetNullBool", true);
        //    value = this.currentStore.Get<bool?>("SetNullBool");
        //    Assert.Equal(true, value);

        //    this.currentStore.Set<bool?>("SetNullBool", false);
        //    value = this.currentStore.Get<bool?>("SetNullBool");
        //    Assert.Equal(false, value);

        //    this.currentStore.Set<bool?>("SetNullBool", null);
        //    value = this.currentStore.Get<bool?>("SetNullBool");
        //    Assert.Null(value);
        //}
    }
}
