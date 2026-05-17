# Object Property

As the game client is written in an [OOP paradigm](https://en.wikipedia.org/wiki/Object-oriented_programming), important data may be stored in the form of an object. Since the [distributed message layer](../dml/index.md) is not sufficient enough, the game client employs another internal mechanism known as _object property_ to serialize these types ahead of time if they may pass over the network.

In some cases, a [DML record](../dml/records.md) will contain an unsuspecting `STR` element, which actually represents the binary buffer of a serialized object. For future reference, the documentation will label these strings as `ByteString`.