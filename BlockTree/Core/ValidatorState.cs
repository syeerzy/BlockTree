using BlockTree.Cryptography.ECC;
using BlockTree.IO;
using System.IO;

namespace BlockTree.Core
{
    public class ValidatorState : StateBase, ICloneable<ValidatorState>
    {
        public ECPoint PublicKey;
        public bool Registered;
        public Fixed10 Votes;

        public override int Size => base.Size + PublicKey.Size + sizeof(bool) + Votes.Size;

        public ValidatorState() { }

        public ValidatorState(ECPoint pubkey)
        {
            this.PublicKey = pubkey;
            this.Registered = false;
            this.Votes = Fixed10.Zero;
        }

        ValidatorState ICloneable<ValidatorState>.Clone()
        {
            return new ValidatorState
            {
                PublicKey = PublicKey,
                Registered = Registered,
                Votes = Votes
            };
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            PublicKey = ECPoint.DeserializeFrom(reader, ECCurve.Secp256r1);
            Registered = reader.ReadBoolean();
            Votes = reader.ReadSerializable<Fixed10>();
        }

        void ICloneable<ValidatorState>.FromReplica(ValidatorState replica)
        {
            PublicKey = replica.PublicKey;
            Registered = replica.Registered;
            Votes = replica.Votes;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(PublicKey);
            writer.Write(Registered);
            writer.Write(Votes);
        }
    }
}
