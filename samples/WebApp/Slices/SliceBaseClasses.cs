namespace RazorSlices.Samples.WebApp.Slices;

public abstract class GenericTodosSliceBase<TModel> : RazorSlice<TModel>;

public abstract class DeepGenericTodosSliceBase<TModel> : GenericTodosSliceBase<TModel>;
