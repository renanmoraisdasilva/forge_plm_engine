import { BrowserRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { Button } from './components/ui/Button';
import { EmptyBlock } from './components/ui/States';
import { EcoDetail } from './screens/EcoDetail';
import { EcoForm } from './screens/EcoForm';
import { EcoList } from './screens/EcoList';
import { ProductDetail } from './screens/ProductDetail';
import { ProductsList } from './screens/ProductsList';
import { ActorProvider } from './state/ActorProvider';
import { ApiProvider } from './state/ApiProvider';

function NotFound() {
  const navigate = useNavigate();
  return (
    <EmptyBlock
      title="Page not found"
      description="The route does not exist."
      action={<Button onClick={() => navigate('/products')}>Go to Products</Button>}
    />
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <ActorProvider>
        <ApiProvider>
          <Routes>
            <Route element={<AppShell />}>
              <Route index element={<Navigate to="/products" replace />} />
              <Route path="products" element={<ProductsList />} />
              <Route path="products/:productId" element={<ProductDetail />} />
              <Route path="ecos" element={<EcoList />} />
              <Route path="ecos/new" element={<EcoForm />} />
              <Route path="ecos/:ecoId" element={<EcoDetail />} />
              <Route path="ecos/:ecoId/edit" element={<EcoForm />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </ApiProvider>
      </ActorProvider>
    </BrowserRouter>
  );
}
