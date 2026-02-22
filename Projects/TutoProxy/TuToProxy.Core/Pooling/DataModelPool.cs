using Microsoft.Extensions.ObjectPool;
using TuToProxy.Core.Models;

namespace TuToProxy.Core.Pooling;

public static class DataModelPool<T> where T : DataBaseModel, new() {
    static readonly ObjectPool<T> pool =
        new DefaultObjectPoolProvider().Create(new DataModelPoolPolicy<T>());

    public static T Rent() => pool.Get();

    public static void Return(T model) {
        model.Reset();
        pool.Return(model);
    }
}

internal class DataModelPoolPolicy<T> : PooledObjectPolicy<T> where T : DataBaseModel, new() {
    public override T Create() => new T();

    public override bool Return(T obj) {
        obj.Reset();
        return true;
    }
}
