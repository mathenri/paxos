using System;

namespace _550_Assignment2
{
    public enum MessageType
    {
        PREPARE_REQUEST,
        PROMISE,
        PREPARE_REQUEST_NACK,
        ACCEPT_REQUEST,
        ACCEPTED,
    };

    /*
     * A message sent between nodes.
     */
    [Serializable]
    public class Message
    {
        public Message(MessageType msgType, int senderID, int receiverID, int instanceNumber, int proposalNumber,
            Value val, int highestAcceptedProposalNumber)
        {
            this.msgType = msgType;
            this.senderID = senderID;
            this.receiverID = receiverID;
            this.instanceNumber = instanceNumber;
            this.proposalNumber = proposalNumber;
            this.val = val;
            this.highestAcceptedProposalNumber = highestAcceptedProposalNumber;
        }

        public Tuple<MessageType, int, int, int, int, Value, int> getMessageContent()
        {
            return new Tuple<MessageType, int, int, int, int, Value, int>(this.msgType, this.senderID, this.receiverID,
                this.instanceNumber, this.proposalNumber, this.val, this.highestAcceptedProposalNumber);
        }

        // getters and setters
        int senderID;

        public int SenderID
        {
            get
            {
                return this.senderID;
            }
            set { }

        }

        int receiverID;

        public int ReceiverID
        {
            get
            {
                return this.receiverID;
            }
            set { }
        }

        int instanceNumber;

        public int InstanceNumber
        {
            get
            {
                return this.instanceNumber;
            }

            set { }
        }

        int proposalNumber;

        public int ProposalNumber
        {
            get
            {
                return this.proposalNumber;
            }

            set { }
        }

        MessageType msgType;

        public MessageType MsgType
        {
            get
            {
                return this.msgType;

            }
            set { }
        }

        Value val;
        public Value Val
        {
            get
            {
                return this.val;
            }
            set { }
        }

        int highestAcceptedProposalNumber;

        public int HighestAcceptedProposalNumber
        {
            get
            {
                return this.highestAcceptedProposalNumber;
            }
            set { }
        }
    }
}