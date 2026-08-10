import BackToTop from "@/components/BackToTop";
import CartPageClient from "@/components/CartPageClient";
import Footer from "@/components/Footer";
import Navbar from "@/components/Navbar";

export default function CartPage() {
  return (
    <>
      <Navbar />
      <CartPageClient />
      <Footer />
      <BackToTop />
    </>
  );
}
