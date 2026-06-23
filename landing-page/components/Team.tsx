import { team } from "@/lib/data";
import SectionHeading from "./SectionHeading";
import { PersonIcon } from "./icons";

export default function Team() {
  return (
    <section id="team" className="py-24 bg-charcoal-light">
      <div className="max-w-7xl mx-auto px-6">
        <SectionHeading
          eyebrow="The Experts"
          title="Meet Our"
          highlight="Team"
          subtitle="Passionate artists dedicated to making every client feel their absolute best."
        />

        <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {team.map((member) => (
            <div
              key={member.name}
              className="card-hover group text-center p-6 bg-charcoal border border-white/5 hover:border-gold/30 rounded-2xl"
            >
              <div className="relative mb-5 inline-block">
                <div
                  className={`w-28 h-28 mx-auto rounded-full border-2 transition-colors flex items-center justify-center ${member.avatar}`}
                >
                  <PersonIcon className={`w-14 h-14 ${member.iconColor}`} />
                </div>
                <span
                  className={`absolute bottom-1 right-1 w-4 h-4 rounded-full border-2 border-charcoal block ${
                    member.status === "green" ? "bg-green-400" : "bg-yellow-400"
                  }`}
                />
              </div>
              <h3 className="text-white font-semibold text-lg">{member.name}</h3>
              <p className="text-gold text-xs uppercase tracking-wider mt-1 mb-3">{member.role}</p>
              <p className="text-gray-500 text-sm">{member.bio}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
