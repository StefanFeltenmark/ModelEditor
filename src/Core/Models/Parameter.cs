using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Models
{
    public class Parameter
    {
        public string Name { get; set; }
        public ParameterType Type { get; set; }
        public object? Value { get; set; }
        public bool IsExternal { get; set; }
        
        // UNIFIED: Always use IndexSetNames internally
        public List<string>? IndexSetNames { get; set; }
        
        // DEPRECATED: Backward compatibility property
        public string? IndexSetName 
        { 
            get => IndexSetNames?.FirstOrDefault();
            set 
            {
                if (value != null)
                {
                    IndexSetNames = new List<string> { value };
                }
            }
        }
        
        private Dictionary<int, object>? indexedValues;
        private Dictionary<string, object>? multiDimValues;
        
        // For computed parameters
        public Expression? ComputeExpression { get; set; }
        public bool IsComputed => ComputeExpression != null;

        // Constructors
        public Parameter(string name, ParameterType type, object value)
        {
            Name = name;
            Type = type;
            Value = value;
            IsExternal = false;
        }

        // Single-dimensional constructor (backward compatible)
        public Parameter(string name, ParameterType type, string indexSetName, bool isExternal)
        {
            Name = name;
            Type = type;
            IndexSetNames = new List<string> { indexSetName }; // Normalize to list
            IsExternal = isExternal;
            indexedValues = new Dictionary<int, object>();
        }

        // Multi-dimensional constructor
        public Parameter(string name, ParameterType type, List<string> indexSetNames, bool isExternal)
        {
            Name = name;
            Type = type;
            IndexSetNames = indexSetNames;
            IsExternal = isExternal;
            
            if (indexSetNames.Count == 1)
            {
                // Single-dimensional, use simple dictionary
                indexedValues = new Dictionary<int, object>();
            }
            else
            {
                // Multi-dimensional, use composite key dictionary
                multiDimValues = new Dictionary<string, object>();
            }
        }

        // Computed parameter constructor
        public Parameter(string name, ParameterType type, List<string> indexSetNames, Expression computeExpression)
        {
            Name = name;
            Type = type;
            IndexSetNames = indexSetNames;
            ComputeExpression = computeExpression;
            IsExternal = false;
            multiDimValues = new Dictionary<string, object>();
        }

        // Properties
        public bool IsIndexed => IndexSetNames != null && IndexSetNames.Count > 0;
        public bool IsScalar => !IsIndexed;
        public bool IsTwoDimensional => IndexSetNames?.Count == 2;
        public bool IsMultiDimensional => IndexSetNames != null && IndexSetNames.Count > 1;
        public int Dimensionality => IndexSetNames?.Count ?? 0;

        public string? SecondIndexSetName 
        { 
            get 
            {
                if (IndexSetNames != null && IndexSetNames.Count >= 2)
                    return IndexSetNames[1];
                return null;
            }
        }

        public bool HasValue 
        { 
            get 
            {
                if (IsScalar)
                    return Value != null;
                
                if (IsIndexed)
                {
                    if (Dimensionality == 1)
                        return indexedValues != null && indexedValues.Count > 0;
                    else
                        return multiDimValues != null && multiDimValues.Count > 0;
                }
                
                return false;
            }
        }

        // Single-dimensional value access
        public void SetIndexedValue(int index, object value)
        {
            if (Dimensionality == 1)
            {
                // Single-dimensional: use simple dictionary
                if (indexedValues == null)
                    indexedValues = new Dictionary<int, object>();
                
                indexedValues[index] = value;
            }
            else if (Dimensionality > 1)
            {
                // Multi-dimensional with single index - error
                throw new InvalidOperationException(
                    $"Parameter '{Name}' is {Dimensionality}-dimensional. Use SetMultiDimValue() or provide all indices.");
            }
            else
            {
                throw new InvalidOperationException($"Parameter '{Name}' is scalar, not indexed");
            }
        }

        public object? GetIndexedValue(int index)
        {
            if (Dimensionality == 1)
            {
                return indexedValues?.TryGetValue(index, out var value) == true ? value : null;
            }
            else if (Dimensionality > 1)
            {
                throw new InvalidOperationException(
                    $"Parameter '{Name}' is {Dimensionality}-dimensional. Use GetMultiDimValue().");
            }
            
            return null;
        }

        // Two-dimensional value access (backward compatibility overloads)
        public void SetIndexedValue(int index1, int index2, object value)
        {
            if (Dimensionality != 2)
            {
                throw new InvalidOperationException(
                    $"Parameter '{Name}' is {Dimensionality}-dimensional, not 2-dimensional");
            }
            
            SetMultiDimValue(new[] { index1, index2 }, value);
        }

        public object? GetIndexedValue(int index1, int index2)
        {
            if (Dimensionality != 2)
            {
                throw new InvalidOperationException(
                    $"Parameter '{Name}' is {Dimensionality}-dimensional, not 2-dimensional");
            }
            
            return GetMultiDimValue(new[] { index1, index2 });
        }

        // Multi-dimensional value access
        public void SetMultiDimValue(int[] indices, object value)
        {
            if (Dimensionality == 0)
            {
                throw new InvalidOperationException($"Parameter '{Name}' is scalar, not indexed");
            }
            
            if (Dimensionality == 1)
            {
                // Single-dimensional: use simple storage
                if (indices.Length != 1)
                    throw new ArgumentException($"Expected 1 index, got {indices.Length}");
                
                SetIndexedValue(indices[0], value);
            }
            else
            {
                // Multi-dimensional: use composite key
                if (indices.Length != Dimensionality)
                {
                    throw new ArgumentException(
                        $"Parameter '{Name}' requires {Dimensionality} indices, got {indices.Length}");
                }
                
                if (multiDimValues == null)
                    multiDimValues = new Dictionary<string, object>();
                
                string key = string.Join(",", indices);
                multiDimValues[key] = value;
            }
        }

        public void SetMultiDimValue(List<int> indices, object value)
        {
            SetMultiDimValue(indices.ToArray(), value);
        }

        public object? GetMultiDimValue(int[] indices)
        {
            if (Dimensionality == 0)
            {
                throw new InvalidOperationException($"Parameter '{Name}' is scalar, not indexed");
            }
            
            if (Dimensionality == 1)
            {
                if (indices.Length != 1)
                    throw new ArgumentException($"Expected 1 index, got {indices.Length}");
                
                return GetIndexedValue(indices[0]);
            }
            else
            {
                if (indices.Length != Dimensionality)
                {
                    throw new ArgumentException(
                        $"Parameter '{Name}' requires {Dimensionality} indices, got {indices.Length}");
                }
                
                if (multiDimValues == null)
                    return null;
                
                string key = string.Join(",", indices);
                return multiDimValues.TryGetValue(key, out var value) ? value : null;
            }
        }

        public object? GetMultiDimValue(List<int> indices)
        {
            return GetMultiDimValue(indices.ToArray());
        }

        // Evaluate computed parameter
        public object? EvaluateComputed(ModelManager manager, int[] indices)
        {
            if (!IsComputed || ComputeExpression == null)
                return null;

            // Check cache first
            if (multiDimValues != null)
            {
                string key = string.Join(",", indices);
                if (multiDimValues.TryGetValue(key, out var cachedValue))
                    return cachedValue;
            }

            // Evaluate expression (would need context support)
            return null;
        }
    }

    public enum ParameterType
    {
        Float,
        Integer,
        String,
        Boolean
    }
}