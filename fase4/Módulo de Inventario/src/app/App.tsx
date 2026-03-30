import { useState } from 'react';
import InventoryHome from './components/InventoryHome';
import CategoryDetail from './components/CategoryDetail';
import ProductDialog from './components/ProductDialog';

export type Category = {
  id: number;
  name: string;
  description: string;
  color: string;
  productsCount: number;
  stockTotal?: string;
  lowStockCount: number;
};

export type Product = {
  code: string;
  name: string;
  stock: number;
  minimum: number;
  unit: string;
  price: number;
  location: string;
};

export default function App() {
  const [currentView, setCurrentView] = useState<'home' | 'category'>('home');
  const [selectedCategory, setSelectedCategory] = useState<Category | null>(null);
  const [showProductDialog, setShowProductDialog] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);

  const handleCategoryClick = (category: Category) => {
    setSelectedCategory(category);
    setCurrentView('category');
  };

  const handleBackToHome = () => {
    setCurrentView('home');
    setSelectedCategory(null);
  };

  const handleNewProduct = () => {
    setSelectedProduct(null);
    setShowProductDialog(true);
  };

  const handleEditProduct = (product: Product) => {
    setSelectedProduct(product);
    setShowProductDialog(true);
  };

  const handleCloseDialog = () => {
    setShowProductDialog(false);
    setSelectedProduct(null);
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-8" style={{ backgroundColor: '#F8FAFC' }}>
      <div className="relative" style={{ width: '1200px', height: '800px' }}>
        {currentView === 'home' && (
          <InventoryHome onCategoryClick={handleCategoryClick} />
        )}
        
        {currentView === 'category' && selectedCategory && (
          <CategoryDetail 
            category={selectedCategory}
            onBack={handleBackToHome}
            onNewProduct={handleNewProduct}
            onEditProduct={handleEditProduct}
          />
        )}

        {showProductDialog && (
          <ProductDialog 
            category={selectedCategory}
            product={selectedProduct}
            onClose={handleCloseDialog}
          />
        )}
      </div>
    </div>
  );
}
