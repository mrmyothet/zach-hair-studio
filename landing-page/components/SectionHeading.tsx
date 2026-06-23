type Props = {
  eyebrow: string;
  title: string;
  highlight: string;
  subtitle: string;
};

export default function SectionHeading({ eyebrow, title, highlight, subtitle }: Props) {
  return (
    <div className="text-center mb-16">
      <p className="text-gold text-xs uppercase tracking-widest mb-3">{eyebrow}</p>
      <h2 className="font-serif text-4xl md:text-5xl text-white mb-4">
        {title} <span className="gold-gradient">{highlight}</span>
      </h2>
      <p className="text-gray-400 max-w-xl mx-auto">{subtitle}</p>
    </div>
  );
}
