using WizUnraveler.DML;

namespace Imlight.Net
{
    public class Session
    {
        /// <summary>
        /// The unique ID for this session.
        /// </summary>
        public ushort SessionID { get; }
        
        /// <summary>
        /// Has the player authenticated their account?
        /// </summary>
        public bool IsAuthenticated { get; set; }
        
        /// <summary>
        /// Is the player currently in queue to connecting to a server?
        /// </summary>
        public bool IsInQueue { get; set; }
        
        /// <summary>
        /// The player's position in queue to a server.
        /// </summary>
        public ushort QueuePosition { get; set; }
        
        /// <summary>
        /// The message that is sent to the active socket once the player has finally left the queue.
        /// </summary>
        public INetworkMessage DequeueMessage { get; set; }

        public Session(ushort sessionId)
        {
            this.SessionID = sessionId;
        }
    }
}