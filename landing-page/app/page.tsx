import Navbar from "@/components/Navbar";
import Hero from "@/components/Hero";
import Services from "@/components/Services";
import Gallery from "@/components/Gallery";
import Team from "@/components/Team";
import Reviews from "@/components/Reviews";
import Contact from "@/components/Contact";
import Footer from "@/components/Footer";
import BackToTop from "@/components/BackToTop";
import { fetchServices } from "@/lib/services";

const HOMEPAGE_SERVICE_COUNT = 6;

export default async function Home() {
  const services = await fetchServices();
  const homepageServices = services
    .toSorted((a, b) => a.displayOrder - b.displayOrder)
    .slice(0, HOMEPAGE_SERVICE_COUNT);

  return (
    <>
      <Navbar />
      <main>
        <Hero />
        <Services services={homepageServices} />
        <Gallery />
        <Team />
        <Reviews />
        <Contact />
      </main>
      <Footer />
      <BackToTop />
    </>
  );
}
