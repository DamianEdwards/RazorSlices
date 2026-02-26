using Models = RazorSlices.Samples.WebApp.Models;

namespace RazorSlices.Samples.WebApp.Slices;

public abstract class TodoSliceBase : RazorSlice<Models.Todo>;

public abstract class GenericTodosSliceBase<TModel> : RazorSlice<TModel>;

public abstract class DeepGenericTodosSliceBase<TModel> : GenericTodosSliceBase<TModel>;
