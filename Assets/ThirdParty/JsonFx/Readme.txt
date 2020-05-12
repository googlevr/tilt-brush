JsonFx.NET - JSON Serializer

The JsonFx.NET JSON parser/serializer functions similarly to the XmlSerializer in .NET

Serializes and deserializes any Dictionary<String, T> and IDictionary with
String keys directly as a JSON object

Serializes and deserializes any List<T>, ArrayList, LinkedList<T>, Queue<T> and
many other IEnumerable types directly as JSON arrays

Serializes and deserializes DateTime, Enum, Nullable<T>, Guid and other
common .NET Types directly as JSON primitives

Serializes and deserializes strongly-typed custom classes (similarly to XML
Serialization in .NET Framework)

Serializes C# 3.0 Anonymous Types directly as JSON objects

Serializes C# 3.0 LINQ Queries as JSON arrays of objects (by enumerating the results)

Follows Postel's Law ("Be conservative in what you do; be liberal in what you accept from others.")
by accepting handling many non-JSON JavaScript concepts:
	- Common literals such as "Infinity", "NaN", and "undefined"
	- Ignores block and line comments when deserializing

Optional ability to control serialization via attributes/interfaces:

	JsonFx.Json.IJsonSerializable:
	Interface which allows classes to control their own JSON serialization

	JsonFx.Json.JsonIgnoreAttribute:
	Attribute which designates a property or field to not be serialized

	System.ComponentModel.DefaultValueAttribute:
	Member does not serialize if the value matches the DefaultValue attribute

	JsonFx.Json.JsonNameAttribute:
	Attribute which specifies the naming to use for a property or field when serializing

	JsonFx.Json.JsonSpecifiedPropertyAttribute:
	Attribute which specifies the name of the property which specifies if member should be serialized

Optional Type-Hinting improves deserializing to strongly-typed objects

	JsonFx.Json.JsonWriter.TypeHintName & JsonFx.Json.JsonReader.TypeHintName:
	Property designates the name of the type hint property (e.g. "__type") and enables type hinting

Optional PrettyPrint mode helps with debugging / human-readability

	JsonFx.Json.JsonWriter.PrettyPrint

Optional custom DateTime serialization override

	JsonFx.Json.JsonWriter.DateTimeSerializer
