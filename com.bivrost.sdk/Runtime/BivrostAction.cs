namespace Bivrost
{
    /// <summary>
    /// An instructor -> student action received over the connection, or a
    /// student -> instructor notification about to be sent. Key matches an
    /// action defined on the Bivrost web platform's Actions page.
    /// </summary>
    public class BivrostAction
    {
        public string Key { get; }

        /// <summary>Raw JSON of the action's payload, if any. Deserialize
        /// into whatever shape your own action expects — the SDK doesn't
        /// enforce a schema.</summary>
        public string PayloadJson { get; }

        public BivrostAction(string key, string payloadJson = null)
        {
            Key = key;
            PayloadJson = payloadJson;
        }
    }
}